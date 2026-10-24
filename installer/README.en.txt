Milli — a voice assistant on top of OpenCode
============================================

Milli is a small tray window: you speak into the microphone, she recognizes the
speech, sends the text to OpenCode and shows the answer. This version is written
in C# and does not need Python: speech recognition runs inside the program.

The Russian version of this file is README.ru.txt.


What you need
-------------
1. Windows 10 or 11 (64-bit).
2. The OpenCode app installed and signed in — Milli talks to it. If OpenCode is
   not running there will be no answers: the window shows a connection error.
3. A microphone (built-in or external).
4. A processor with AVX support (Intel since 2011, AMD FX/Ryzen and newer).
   On very old processors recognition may not start.


How to use it
-------------
* Milli starts and hides in the tray straight away (near the clock). Double-click
  the icon to show or hide the window.
* To talk: hold the voice key (Num 9 by default), speak, then release. Milli
  recognizes the speech and sends the text right away.
* To show or hide the window: the window key (Num 8 by default).
* You can also type: enter your text in the box and press Enter.
* The “🎤 Voice” button works like the key: hold it with the mouse while you speak.

Keys, model and language are changed in the tray (right-click the icon):
  “Hotkeys…”        — assign your own keys;
  “AI model…”       — which model Milli answers with;
  “🎧 Speech model…” — which speech recognition model to use;
  “Voice → send immediately” — send the recognized text at once or put it in the box;
  “Language” → “Русский” / “English” — the interface language.


Stop, commands and reminders
----------------------------
* “⏹ Stop” — a button in the window and a tray item: interrupt a long answer.
  The same by voice: “stop”, “enough”, “cancel”.
* Repeat the last answer: “repeat”, “say it again”.
* Start a clean conversation: “new conversation”, “clear chat” — the history
  is cleared.

Reference commands run right away, on the spot, without asking the model,
and are understood in both languages regardless of the selected one:

  “what time is it”, “what's the date”, “what day is it”
  “open notepad”, “open chrome”, “open documents”, «открой загрузки»

Applications come from a fixed whitelist (Opera GX, Chrome, Edge, Discord,
Telegram, Steam, Epic, OpenCode, qBittorrent, FileZilla, WinRAR, 7-Zip,
LM Studio, IntelliJ and the standard Windows tools), plus the “Documents”,
“Downloads” and “Desktop” folders. An unknown name is passed to the model
as an ordinary question.

Reminders:

  “remind me in 10 minutes to call mom”
  “remind me in half an hour to turn off the stove”
  «напомни через 2 часа проверить почту»

Minutes, hours, “half an hour”, “an hour and a half” and combined times are
understood. The reminder arrives as a tray balloon and in the conversation.

Ordinary text containing these words (“how do I open a document”) is not
treated as a command and goes to the model as usual.


Language (Russian and English)
------------------------------
Pick the language in the tray: “Language” → “Русский” or “English”. Everything
changes at once:

* the labels in the window, in the tray menu and in the settings dialogs;
* the recognition language (Milli listens in Russian or in English);
* the language Milli answers in;
* Milli's name in the conversation.

The choice is saved in config.json (the language key) and survives program
updates. If you edited the persona (the persona key) yourself, it stays as you
wrote it — only the built-in default text is replaced.


Two recognition models
----------------------
Two models ship with the program, in the model folder next to it:

* ggml-base-q5_1.bin  — fast (about 4 seconds on a weak laptop), basic accuracy.
  Used by default;
* ggml-small-q5_1.bin — more accurate but noticeably slower (up to 15 seconds
  per phrase on a weak processor).

To switch: tray → “🎧 Speech model…”. You can add your own files: put any
ggml-*.bin into the model folder and it appears in the list.


Settings
--------
The config.json file sits next to the program (in the install folder). It holds:

  title          — Milli's label in the conversation;
  model          — the AI model (id and providerID from OpenCode);
  language       — language of the interface, recognition and answers (ru or en);
  sttModel       — the recognition model file from the model folder;
  autoSendVoice  — send recognized speech at once (true) or put it in the box (false);
  startHidden    — start hidden in the tray (true);
  cleanSessions  — at start, remove Milli's old sessions from OpenCode (true);
  hotkeyVoice    — the “hold to talk” key, for example Num9 or Ctrl+F7;
  hotkeyWindow   — the show/hide window key, for example Num8 or Ctrl+F8;
  persona        — Milli's character, a service instruction for the model.

Milli's old conversations are removed from OpenCode at every start — only the
current session stays. Turn it off with: cleanSessions = false.

If the program folder is not writable, the settings move to
%LOCALAPPDATA%\Milli\config.json — edit whichever file exists.


Checking recognition
--------------------
If Milli seems to mishear you, you can test recognition on a ready wav file (no
microphone needed). Open a command prompt in the install folder:

  Milli.exe --say-file "C:\path\to\file.wav"

The recognized text appears in the input box or goes straight to OpenCode,
depending on your settings. The same command is handy for trying new models.

Service switches (for testing, not needed for everyday use):

  Milli.exe --lang en              — switch to English once;
  Milli.exe --menu                 — open the settings menu right away (like a right-click on the icon);
  Milli.exe --model                — open the AI model picker right away;
  Milli.exe --stt                  — open the speech model picker;
  Milli.exe --clean-sessions      — remove Milli's old sessions once (count in the status line);
  Milli.exe --keys                 — open the hotkey settings.


If something does not work
--------------------------
* The keys do nothing. Another program holds them — tray → “Hotkeys…” and pick
  others. While Milli's window is open the keys work anyway.
* “OpenCode is not answering”. Make sure the OpenCode app is running and signed
  in. If OpenCode was restarted, Milli finds it again on its own — wait a couple
  of seconds and try again.
* An answer like “Insufficient account funds” is about a paid model. Tray →
  “AI model…” and pick a free one.
* Nothing is heard. Check that the microphone works and is allowed in
  Settings → Privacy → Microphone.
* Milli hears you but writes nonsense. Check that the language (tray →
  “Language”) matches the language you speak.


Uninstalling
------------
Start menu → “Uninstall Milli”, or Settings → Apps. The settings and models are
removed together with the program.
