# Granite Local Embedding Experiment Tool

This loopback-only, experiment-only service exposes the existing OpenAI-compatible `POST /v1/embeddings` contract for `ibm-granite/granite-embedding-97m-multilingual-r2`. It is not a UniPM production component.

The official model metadata specifies CLS pooling, L2 normalization, empty query/document prompts, and a 32,768-token maximum context. The service therefore uses the same documented encoding for query and document inputs; it does not infer an unannounced prefix.

Install dependencies in an ignored virtual environment, set a local model path, then run `./Start-GraniteEmbeddingServer.ps1 -PythonPath <python> -ModelPath <model> -Offline`. It binds to `127.0.0.1` by default and never requires a key.
