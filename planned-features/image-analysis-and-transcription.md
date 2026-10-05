# Feasibility Assessment: Image Analysis and Voice Transcription in tawk-mcp

## 1. Executive Summary

This document details the feasibility of integrating **Image Analysis** and **Voice Transcription (using local models such as Whisper)** into `tawk-mcp`.

- **Voice Transcription (Local Whisper)**: **Feasible**. Can be implemented via in-process .NET bindings (`Whisper.net` backed by `whisper.cpp`), an external local daemon (e.g. `faster-whisper` / `whisper.cpp` server), or CLI execution.
- **Image Analysis (Vision)**: **Feasible**. Can be achieved either by passing image data directly to the client LLM via native MCP `ImageContent` blocks (recommended, zero-overhead), or by querying a local vision model (e.g. Ollama with LLaVA/MiniCPM-V or ONNX Runtime).
- **Core Dependency**: Both features require resolving access to raw media files or audio/image buffers, which are managed by the upstream `tawk` client.

---

## 2. Architecture Context & Data Flow

`tawk-mcp` is an MCP server written in .NET 10 that interfaces with `tawk` (the terminal WhatsApp client) over a Unix domain socket using JSON-RPC control protocol messages.

```
+-------------------------------------------------------------+
|                      MCP Client                             |
|          (Claude Code, Claude Desktop, VS Code)             |
+-------------------------------------------------------------+
                              | MCP (HTTP / Stdio)
                              v
+-------------------------------------------------------------+
|                      tawk-mcp (.NET 10)                     |
|                                                             |
|  [Clients] -> [Managers] -> [Engines] -> [ResourceAccess]   |
+-------------------------------------------------------------+
                              | Unix Domain Socket (JSON-RPC)
                              v
+-------------------------------------------------------------+
|                        tawk (C Client)                      |
|                                                             |
|  - SQLite Message Store (holds media_path)                  |
|  - Media Cache & Download Engine                            |
|  - Whatsmeow / Gateway to WhatsApp                          |
+-------------------------------------------------------------+
```

---

## 3. The Core Prerequisite: Local Media File Resolution

When a WhatsApp message contains media (photo, voice note, video, or document):
1. `read_messages` in `tawk` returns message metadata:
   - `type`: `"image"`, `"audio"`, `"video"`, `"document"`, etc.
   - `text`: Caption (if any).
   - Currently, `media_path` is **not** exposed in the `control_codec_message` JSON output.
2. `download_media` triggers `messaging_manager_fetch_media`, but returns `{}` without a file path or callback notification.

### Resolution Options
* **Option A (Preferred - Upstream Enhancement in `tawk`)**:
  - Update `control_codec_message` and/or `download_media` in `tawk` to include `media_path` when the media is downloaded.
  - Or add a dedicated control operation (e.g., `get_media_path` or `get_media_info`).
* **Option B (Zero-Change to `tawk` - Direct SQLite Lookup)**:
  - `tawk-mcp` already runs under the same user account and can access `tawk`'s local state directory.
  - `tawk`'s SQLite database (`tawk.db`) stores `media_path` in the `messages` table (`COLUMNS: id, chat_jid, ..., media_path, type...`).
  - `tawk-mcp` can query the database directly or read the media cache directory when media is marked available.

---

## 4. Voice Transcription (Local Whisper)

### Approaches
1. **In-Process via `Whisper.net`**:
   - Uses native `whisper.cpp` bindings for .NET.
   - Runs directly within `tawk-mcp` without extra running daemons.
   - Supports CPU (AVX2/AVX512) and GPU acceleration (CUDA, Vulkan, Metal).
   - Compatible with quantized GGML models (`tiny`, `base`, `small`).
   - *Requirement*: Audio conversion. WhatsApp voice notes are usually `.ogg` (Opus encoded). Whisper expects 16kHz WAV/PCM audio, requiring a lightweight transcoding step (e.g., `ffmpeg` or managed audio decoder).
2. **Local HTTP Daemon (e.g. `whisper.cpp` server, `faster-whisper`, or Ollama)**:
   - `tawk-mcp` delegates the transcription to a local REST endpoint (e.g. `http://localhost:8080/inference` or `http://localhost:11434`).
   - Keeps `tawk-mcp` free of heavy native binaries and C++ runtime dependencies.
3. **Subprocess / CLI Execution**:
   - Invokes `whisper` CLI directly on the downloaded audio file and reads stdout.

### Proposed Tooling in `tawk-mcp`
- `transcribe_message`: Transcribes a specific audio/voice message by `messageId`.
- Optional auto-transcription parameter or configuration setting for `read_messages`.

---

## 5. Image Analysis (Vision)

### Approaches
1. **Native MCP Image Content Return (Recommended)**:
   - MCP natively supports returning base64-encoded binary images in tool responses:
     ```json
     {
       "content": [
         {
           "type": "image",
           "data": "<base64>",
           "mimeType": "image/jpeg"
         }
       ]
     }
     ```
   - The MCP client (Claude Code, Claude Desktop, etc.) receives the image directly and processes it using its native multimodal vision capabilities.
   - Requires zero local vision model footprint, zero GPU memory overhead, and minimal implementation complexity.
2. **Local Vision Inference**:
   - If an offline-only guarantee is required without sending image bytes to the MCP client's LLM, `tawk-mcp` can query a local vision model (e.g. Ollama with LLaVA/MiniCPM-V or Microsoft Florence/ONNX) and return the resulting textual description/OCR.

### Proposed Tooling in `tawk-mcp`
- `view_message_image`: Fetches and returns the image content as an MCP image block for visual inspection.
- `analyze_image`: Runs local OCR or image description if local-only vision is requested.

---

## 6. Recommendations & Next Steps

1. **Step 1: Media Path Resolution**
   - Provide a mechanism in `ResourceAccess` to resolve `media_path` for a given `message_id` (via `tawk` control protocol or SQLite fallback).
2. **Step 2: MCP Image Inspection Tool**
   - Add tool to read local image bytes, detect MIME type, and return an MCP `ImageContent` object to the client.
3. **Step 3: Whisper Integration Architecture**
   - Choose between `Whisper.net` (embedded) vs. local HTTP inference service.
   - Implement audio format normalization (transcode `.ogg` to 16kHz WAV).
