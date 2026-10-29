# Audio is uploaded after the Release, not streamed while speaking

The requirement is minimum delay, and OpenAI's Realtime API can transcribe while the user is still speaking, so most of the Transcript would exist at Release. The tool nevertheless records locally and POSTs the whole WAV to the transcription endpoint when the key is released. As of October 2026 the file endpoint with `gpt-transcribe` costs a quarter of the streaming model per minute ($0.0045 against $0.017 for `gpt-live-transcribe`), is one multipart request over the BCL's `HttpClient` rather than a WebSocket session with a 60-minute lifetime and its own event protocol, and FreeFlow ships the same default with streaming off. The TLS connection is opened on key-down, at least half a second before Release, so the handshake overlaps the speech and the wait after Release is upload plus model time.

## Consequences

- The wait after Release is expected around a second and is unmeasured; OpenAI publishes no latency numbers for either path. It is logged per Dictation so the choice can be revisited on data.
- The transport sits behind one interface in the adapter so a streaming implementation can be added as a config option without touching the core. A Realtime session needs 24 kHz PCM where the upload uses 16 kHz; the microphone capture lets the audio engine resample, so the rate is a parameter.
- Audio stays in memory. The endpoint's 25 MB ceiling is about 13 minutes of 16 kHz mono, far above the 120-second Listening cap.
