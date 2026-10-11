# Phish Analyser

Phish Analyser is a Windows tool that reads email headers and shows a Grammarly-style floating bubble with the number of suspicious signals in the message being read. It analyses mail locally in a shared rule engine with measured precision, reading headers from Outlook (classic and new) and webmail surfaces.

Build and test the C# engine and CLI from `engine-dotnet/`; see the docs in `docs/` and the build plan for the phase roadmap.

Current status: Phase 0 scaffolded — repository structure, CI skeleton and protection files in place; Phase 1 (parser and corpus) is next.