# Changelog

Releases of `uSync.Cli`, `uSync.Command.Setup` and `uSync.Command.Notify`.

## 17.3.0 - 2026-11-27

### New

Deploy commands
* `server-status` shows the runtime level and version, and exits with 1 unless the site is running.
* `package-migrations` lists pending package migrations and can run them. It also takes `--json`.
* `log-errors` lists recent errors from the log viewer and exits with 1 if there are any.
* `cache-status` and `cache-rebuild --wait`.
* `healthcheck-run-all` runs every health check, with `--fail-on` to choose what fails the step.

Diagnostic commands
* `server-troubleshoot` gathers site details, with `--markdown` for pasting into an issue.
* `redirects-list` and `webhook-list`.
* `indexer-list` shows index health, and `--fail-unhealthy` exits with 1 when an index is unhealthy.

Content checks
* `content-urls` lists published documents without a working URL.
* `recyclebin-list` lists the recycle bin, and `--empty --yes` empties it.

Offline file commands (no site needed)
* `files-diff` lists the uSync items changed since a git ref, as text, JSON or markdown.
* `files-find` finds the files for an item by key, alias or file name.
* `files-clean` lists delete markers with their age, and `--older-than --remove` removes old ones.

Other
* `config-check` shows the connection settings, where each one came from, and tries to sign in.
* `--json` output for the list commands.

## 17.2.1 - 2026-11-24

### New
* **uSync.Command.Notify**, a new package for the site. It adds a "uSync Site Started" webhook event that fires once per new build, with a GitHub `repository_dispatch` shaped payload, so a deploy pipeline can run the import when the site says it's up instead of waiting for the restart.
* The webhook can name a token (`uSync-Notify-Token` header), which the package sends as the `Authorization` header, read from `uSync:Notify:Secrets` on the site. It is only sent to hosts in `uSync:Notify:AllowedHosts` (default `api.github.com`).
* `files-webhook --repo owner/name` writes the webhook into your uSync folder.

## 17.2.0 - 2026-11-23

### New
* `files-verify` checks a uSync folder before you import it: invalid XML, merge conflict markers, duplicate keys, alias clashes, broken references and unknown files.
* `usync-report` reports what an import would change without running it. On GitHub Actions it also writes a step summary.
* `usync-ping --timeout` waits for a site to come up.
* The server and credentials can come from environment variables.

### Changed
* `healthcheck-group-check` exits with 1 when the results contain errors.
* `usync-ping` fails straight away when the site rejects the credentials.
* Errors print as one line rather than a stack trace. Set `USYNC_DEBUG` for the full trace.
* The tool rolls forward to newer .NET runtimes.

## 17.1.1 - 2026-11-12

### Fixed
* Follow background runs instead of exiting.

## 17.1.0 - 2026-11-11

### Changed
* `usync-import` and `usync-export` report progress and failures.
* Package updates.

### Fixed
* `usync-ping` reports the result of its last attempt correctly.

## 17.0.0 - 2026-08-26

* Umbraco and uSync v17 (net10.0).
* New `uSync.Command.Setup` package.
* If the server URL isn't passed in, the tool looks for it in config.
