Thanks for your interest in contributing to SwimStats!

How to contribute

- Fork the repository and create a feature branch from `main`.
  - Naming: `feature/<short-description>` or `fix/<short-description>`.
- Make small, focused changes with clear commit messages.
- Run the test suite locally before creating a pull request.

Build & test locally (PowerShell on Windows):

```powershell
# From repository root
dotnet restore
dotnet build
dotnet test
```

Branch and PR policy

- Work on a topic branch, open a PR against `main`.
- PRs should include a short description, testing notes, and screenshots if UI changes.
- CI will run build and tests automatically. Address any failing tests before merging.

Coding standards

- Follow existing repository structure and naming conventions.
- Keep UI changes accessible (color-blind friendly palettes already used).
- Add unit tests for new logic where applicable.

Maintainers

- If a PR is not acted on within a few days, mention a maintainer or open an issue for discussion.

Thanks — contributions are welcome!