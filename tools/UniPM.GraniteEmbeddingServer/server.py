"""Loopback-only OpenAI-compatible server for the local Granite experiment."""
from __future__ import annotations

import argparse
import json
import logging
import math
import os
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Protocol

MODEL_NAME = "ibm-granite/granite-embedding-97m-multilingual-r2"
DIMENSIONS = 384


class Encoder(Protocol):
    def encode(self, inputs: list[str]) -> list[list[float]]: ...


class GraniteEncoder:
    def __init__(self, model_path: str, max_tokens: int, offline: bool) -> None:
        import torch
        from transformers import AutoModel, AutoTokenizer

        self.torch = torch
        self.max_tokens = max_tokens
        self.tokenizer = AutoTokenizer.from_pretrained(model_path, local_files_only=offline)
        self.model = AutoModel.from_pretrained(model_path, local_files_only=offline)
        self.model.eval()

    def encode(self, inputs: list[str]) -> list[list[float]]:
        batch = self.tokenizer(inputs, padding=True, truncation=True, max_length=self.max_tokens, return_tensors="pt")
        with self.torch.inference_mode():
            # Official model metadata specifies CLS pooling and L2 normalization.
            vectors = self.model(**batch).last_hidden_state[:, 0]
            vectors = self.torch.nn.functional.normalize(vectors, p=2, dim=1)
        return [[float(value) for value in vector] for vector in vectors]


class EmbeddingApplication:
    def __init__(self, encoder: Encoder | None, max_batch_size: int, max_input_characters: int) -> None:
        self.encoder = encoder
        self.max_batch_size = max_batch_size
        self.max_input_characters = max_input_characters

    def embed(self, request: object) -> tuple[int, object]:
        if self.encoder is None:
            return HTTPStatus.SERVICE_UNAVAILABLE, {"error": "model_not_ready"}
        if not isinstance(request, dict) or request.get("model") != MODEL_NAME:
            return HTTPStatus.BAD_REQUEST, {"error": "unsupported_request"}
        value = request.get("input")
        inputs = [value] if isinstance(value, str) else value
        if not isinstance(inputs, list) or not (1 <= len(inputs) <= self.max_batch_size):
            return HTTPStatus.BAD_REQUEST, {"error": "invalid_batch"}
        if any(not isinstance(item, str) or not item.strip() or len(item) > self.max_input_characters for item in inputs):
            return HTTPStatus.BAD_REQUEST, {"error": "invalid_input"}
        try:
            vectors = self.encoder.encode(inputs)
        except Exception:
            logging.exception("Granite embedding execution failed")
            return HTTPStatus.SERVICE_UNAVAILABLE, {"error": "embedding_unavailable"}
        if len(vectors) != len(inputs) or any(len(vector) != DIMENSIONS or not all(math.isfinite(number) for number in vector) for vector in vectors):
            return HTTPStatus.SERVICE_UNAVAILABLE, {"error": "invalid_embedding_output"}
        return HTTPStatus.OK, {"object": "list", "data": [{"object": "embedding", "index": index, "embedding": vector} for index, vector in enumerate(vectors)]}


def create_handler(application: EmbeddingApplication):
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, _format: str, *_args: object) -> None:
            pass
        def _write(self, status: int, body: object) -> None:
            encoded = json.dumps(body, separators=(",", ":")).encode()
            self.send_response(status); self.send_header("Content-Type", "application/json"); self.send_header("Content-Length", str(len(encoded))); self.end_headers(); self.wfile.write(encoded)
        def do_GET(self) -> None:
            if self.path == "/health": self._write(HTTPStatus.OK if application.encoder else HTTPStatus.SERVICE_UNAVAILABLE, {"status": "ready" if application.encoder else "not_ready"})
            else: self._write(HTTPStatus.NOT_FOUND, {"error": "not_found"})
        def do_POST(self) -> None:
            if self.path != "/v1/embeddings": self._write(HTTPStatus.NOT_FOUND, {"error": "not_found"}); return
            try: request = json.loads(self.rfile.read(int(self.headers.get("Content-Length", "0"))))
            except (ValueError, TypeError): self._write(HTTPStatus.BAD_REQUEST, {"error": "malformed_json"}); return
            status, body = application.embed(request); self._write(status, body)
    return Handler


def main() -> None:
    parser = argparse.ArgumentParser(); parser.add_argument("--model-path", required=True); parser.add_argument("--host", default="127.0.0.1"); parser.add_argument("--port", type=int, default=8091); parser.add_argument("--offline", action="store_true"); parser.add_argument("--max-batch-size", type=int, default=16); parser.add_argument("--max-input-characters", type=int, default=4000); parser.add_argument("--max-tokens", type=int, default=32768); args = parser.parse_args()
    if args.host not in ("127.0.0.1", "::1", "localhost"): raise SystemExit("The Granite experiment server binds to loopback only.")
    application = EmbeddingApplication(GraniteEncoder(args.model_path, args.max_tokens, args.offline), args.max_batch_size, args.max_input_characters)
    ThreadingHTTPServer((args.host, args.port), create_handler(application)).serve_forever()

if __name__ == "__main__": main()
