# Claude project entrypoint

Before changing this project, read these shared sources of truth:

1. `AGENTS.md` for product constraints, architecture map, coding invariants and commands.
2. `PROJECT_STATUS.md` for the current milestone, active work, backlog and verification record.
3. `docs/COLLABORATION.md` for task ownership and handoff rules.
4. `README.md` and the relevant architecture, maintenance or validation guide before touching those areas.

Follow `AGENTS.md` as the canonical project policy. Do not create a second, conflicting progress log. Claim work in `PROJECT_STATUS.md` before editing shared files; before finishing, record exact changed paths, commands actually run, results and unverified areas. Re-read the status file before saving a status update so concurrent work is not overwritten.

This file is a Claude-oriented project entrypoint when the coding environment loads `CLAUDE.md`. Standard Claude Desktop conversations may not automatically read local files; use the project instruction/bootstrap in `docs/COLLABORATION.md` and confirm that the session can access the same workspace before assuming the status is current.
