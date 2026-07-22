/// <reference types="node" />
import { BroadcastChannel } from 'worker_threads'
import { ReadableStream, TransformStream, WritableStream } from 'stream/web'

const g = globalThis as unknown as Record<string, unknown>

if (!g.TransformStream) {
  g.TransformStream = TransformStream
}

if (!g.ReadableStream) {
  g.ReadableStream = ReadableStream
}

if (!g.WritableStream) {
  g.WritableStream = WritableStream
}

if (!g.BroadcastChannel) {
  g.BroadcastChannel = BroadcastChannel
}
