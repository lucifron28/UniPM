import http.client
import json
import math
import socket
import sys
import threading
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parents[1]))

from server import DIMENSIONS, EmbeddingApplication, MODEL_NAME, create_handler


class FakeEncoder:
    def __init__(self, vectors=None, failure=None):
        self.vectors = vectors
        self.failure = failure
        self.received = []

    def encode(self, inputs):
        self.received.append(inputs)
        if self.failure:
            raise self.failure
        if self.vectors is not None:
            return self.vectors
        return [[1.0 / math.sqrt(DIMENSIONS)] * DIMENSIONS for _ in inputs]


class ServerHost:
    def __init__(self, application):
        self.server = __import__("http.server", fromlist=["ThreadingHTTPServer"]).ThreadingHTTPServer(("127.0.0.1", 0), create_handler(application))
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)

    def __enter__(self):
        self.thread.start()
        return self

    def __exit__(self, *_):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(timeout=2)

    def request(self, method, path, body=None, headers=None):
        connection = http.client.HTTPConnection("127.0.0.1", self.server.server_port, timeout=2)
        connection.request(method, path, body=body, headers=headers or {})
        response = connection.getresponse()
        payload = json.loads(response.read())
        connection.close()
        return response.status, payload


class ServerContractTests(unittest.TestCase):
    def setUp(self):
        self.encoder = FakeEncoder()
        self.app = EmbeddingApplication(self.encoder, 2, 32)

    def test_health_reports_ready_and_not_ready(self):
        with ServerHost(self.app) as host:
            self.assertEqual((200, {"status": "ready"}), host.request("GET", "/health"))
        with ServerHost(EmbeddingApplication(None, 2, 32)) as host:
            self.assertEqual((503, {"status": "not_ready"}), host.request("GET", "/health"))

    def test_http_preserves_single_and_multiple_input_order(self):
        with ServerHost(self.app) as host:
            status, body = host.request("POST", "/v1/embeddings", json.dumps({"model": MODEL_NAME, "input": ["query", "document"]}), {"Content-Type": "application/json"})
        self.assertEqual(200, status)
        self.assertEqual([["query", "document"]], self.encoder.received)
        self.assertEqual([0, 1], [item["index"] for item in body["data"]])
        self.assertTrue(all(len(item["embedding"]) == DIMENSIONS for item in body["data"]))

    def test_http_rejects_malformed_unsupported_and_excessive_requests(self):
        with ServerHost(self.app) as host:
            self.assertEqual((400, {"error": "malformed_json"}), host.request("POST", "/v1/embeddings", "{"))
            self.assertEqual((400, {"error": "unsupported_request"}), host.request("POST", "/v1/embeddings", json.dumps({"model": "other", "input": "x"})))
            self.assertEqual((400, {"error": "invalid_batch"}), host.request("POST", "/v1/embeddings", json.dumps({"model": MODEL_NAME, "input": ["a", "b", "c"]})))

    def test_http_rejects_invalid_and_oversized_content_lengths(self):
        with ServerHost(self.app) as host:
            self.assertEqual((413, {"error": "request_too_large"}), host.request("POST", "/v1/embeddings", "{}", {"Content-Length": str(self.app.max_request_bytes + 1)}))
            connection = socket.create_connection(("127.0.0.1", host.server.server_port), timeout=2)
            connection.sendall(b"POST /v1/embeddings HTTP/1.1\r\nHost: localhost\r\n\r\n")
            chunks = []
            while chunk := connection.recv(1024):
                chunks.append(chunk)
            response = b"".join(chunks).decode("ascii")
            connection.close()
        self.assertIn("400", response)
        self.assertIn("invalid_content_length", response)

    def test_http_rejects_truncated_request_body(self):
        with ServerHost(self.app) as host:
            connection = socket.create_connection(("127.0.0.1", host.server.server_port), timeout=2)
            connection.sendall(b"POST /v1/embeddings HTTP/1.1\r\nHost: localhost\r\nContent-Length: 10\r\n\r\n{}")
            connection.shutdown(socket.SHUT_WR)
            response = b"".join(iter(lambda: connection.recv(1024), b"")).decode("ascii")
            connection.close()
        self.assertIn("400", response)
        self.assertIn("truncated_request", response)

    def test_http_sanitizes_encoder_failures_and_output_validation(self):
        source_text = "source-abc-123"
        failing = EmbeddingApplication(FakeEncoder(failure=RuntimeError(source_text)), 2, 32)
        invalid_encoders = [
            FakeEncoder(vectors=[]),
            FakeEncoder(vectors=[[1.0] * (DIMENSIONS - 1)]),
            FakeEncoder(vectors=[[float("nan")] * DIMENSIONS]),
            FakeEncoder(vectors=[[float("inf")] * DIMENSIONS]),
        ]
        with self.assertLogs("unipm.granite", level="ERROR") as logs:
            self.assertEqual(
                (503, {"error": "embedding_unavailable"}),
                failing.embed({"model": MODEL_NAME, "input": source_text}),
            )
        self.assertNotIn(source_text, "\n".join(logs.output))
        with ServerHost(failing) as host:
            status, body = host.request("POST", "/v1/embeddings", json.dumps({"model": MODEL_NAME, "input": source_text}))
        self.assertEqual((503, {"error": "embedding_unavailable"}), (status, body))
        self.assertNotIn(source_text, json.dumps(body))
        for encoder in invalid_encoders:
            with ServerHost(EmbeddingApplication(encoder, 2, 32)) as host:
                self.assertEqual((503, {"error": "invalid_embedding_output"}), host.request("POST", "/v1/embeddings", json.dumps({"model": MODEL_NAME, "input": "query"})))


if __name__ == "__main__":
    unittest.main()
