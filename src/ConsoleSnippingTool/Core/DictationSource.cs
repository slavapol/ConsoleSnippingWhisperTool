namespace ConsoleSnippingTool.Core;

/// Where a Dictation's audio comes from (glossary: Source): the user's default communication microphone, or what the
/// computer plays on its default communication output device. Not a SoundSource, which is what a Signal plays.
public enum DictationSource { Microphone, Playback }
