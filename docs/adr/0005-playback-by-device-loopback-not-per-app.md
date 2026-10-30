# Playback is recorded from the communication output device, not from the meeting app

A Playback Dictation transcribes what the other people in a meeting say. Windows offers two ways to record what is playing: device loopback, the mix of everything one output device plays, and process loopback, the audio of one app's process tree (Windows 11 and later, activated asynchronously through `ActivateAudioInterfaceAsync`). The tool uses device loopback on the Default Communication Device: the microphone's WASAPI path with `AUDCLNT_STREAMFLAGS_LOOPBACK` on a render endpoint, nothing for the user to choose, and the same behaviour for Teams, Zoom and a meeting in a browser tab. Process loopback would hear the meeting whichever speaker it plays on and would leave out the tool's own sounds, but the user would have to name the app, a browser meeting means naming the browser, and it adds a second activation path. The communication default rather than the plain Default Device because with a Bluetooth headset in a call the meeting plays on the headset's Hands-Free endpoint, which Windows normally makes the communication default, while the Default Device stays on the silent stereo endpoint; the Microphone uses the same role.

## Consequences

- A meeting app set to a speaker other than the communication default is not heard. The Listening line names the recorded device so the mismatch shows, and a recording in which nothing played at all is never sent ("Nothing heard (nothing played)").
- Everything that plays on that device is recorded: the tool's own Ready signal, notification sounds, a video in another tab.
- Loopback delivers no audio while nothing plays on the device, so the recording holds only what played and can be shorter than the time spent Listening. The gaps are not filled with silence.
- Windows shows no recording indicator, because its microphone icon covers capture devices only. The Ready signal and the cap are the only reminders that Playback is Listening.
