import math
import unittest
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).parents[1]))
from server import DIMENSIONS, EmbeddingApplication, MODEL_NAME

class FakeEncoder:
    def encode(self, inputs): return [[1.0 / math.sqrt(DIMENSIONS)] * DIMENSIONS for _ in inputs]

class ServerContractTests(unittest.TestCase):
    def setUp(self): self.app = EmbeddingApplication(FakeEncoder(), 2, 32)
    def test_single_and_ordered_multiple_inputs(self):
        status, body = self.app.embed({"model": MODEL_NAME, "input": ["query", "document"]})
        self.assertEqual(200, status); self.assertEqual([0, 1], [item["index"] for item in body["data"]]); self.assertEqual(DIMENSIONS, len(body["data"][0]["embedding"]))
    def test_rejects_blank_or_oversized_input(self):
        self.assertEqual(400, self.app.embed({"model": MODEL_NAME, "input": " "})[0]); self.assertEqual(400, self.app.embed({"model": MODEL_NAME, "input": "x" * 33})[0])
    def test_model_not_ready(self): self.assertEqual(503, EmbeddingApplication(None, 2, 32).embed({"model": MODEL_NAME, "input": "test"})[0])
if __name__ == "__main__": unittest.main()
