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

if (typeof HTMLDialogElement !== 'undefined') {
  if (!HTMLDialogElement.prototype.showModal) {
    HTMLDialogElement.prototype.showModal = function showModal() {
      this.setAttribute('open', '')
      this.querySelector<HTMLElement>('[autofocus]')?.focus()
    }
  }

  if (!HTMLDialogElement.prototype.close) {
    HTMLDialogElement.prototype.close = function close() {
      this.removeAttribute('open')
      this.dispatchEvent(new Event('close'))
    }
  }
}
