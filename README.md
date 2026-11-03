# eAdaptor Next Ping

A one-page, open-source tester for **CargoWise eAdaptor Next**: configure the OAuth 2.0
client-certificate authentication (WiseCloud), paste or load a Universal XML file, send it to
CargoWise, read the response. That's it.

Use it when you just want to answer two questions in five minutes:

1. **Does my client certificate + tenant configuration produce a valid OAuth token?**
   - Click *Test authentication* - no XML needed.
2. **Does my message go through?**
   - Paste or load the XML, click *Send to CargoWise*, read the response.

| | |
|---|---|
| **License** | [MIT](LICENSE) |
| **Runtime** | .NET 8 (ASP.NET Core, minimal API + static page) |
| **Front end** | one HTML page + one stylesheet + one vanilla JS file - no npm, no framework |
| **Binds to** | `http://127.0.0.1:5090` (localhost only) |
| **Dependencies** | none (zero NuGet packages) |

> **Official reference:** the authoritative protocol specification is WiseTech Global's
> **eAdaptor Next Developer's Guide** (PDF). It is distributed by WiseTech as part of the CargoWise
> integration documentation - ask your CargoWise account manager or get it from the CargoWise
> documentation portal. Use the guide for complete field-level details; this tool implements the
> common happy path and is a companion to that guide, not a replacement for it.

## Quick start

```
git clone https://github.com/yufangzhao/cargowise-eadaptor-next-ping.git
cd eadaptor-next-ping
dotnet run --project src/EadaptorNextPing
```

The browser opens at `http://127.0.0.1:5090`.

### Configure (matches CargoWise EDI Client Details, Inbound tab)

| Field | Where it comes from |
|---|---|
| Endpoint URL | The **Enterprise code** and **Server code** fields sit above it and drive it live: typing `XYZ` + `TST` turns the URL into `https://XYZTSTservices.wisegrid.net/eAdaptorNext`. Pick the **Mode** (Sync / Async) to target `/eAdaptorNext` or `/eAdaptorNext/Async` - switching adds or removes `/Async` automatically. Custom URLs (e.g. a local listener) also work - just leave the codes empty |
| Token endpoint | The **Authority URL** shown in CargoWise - paste it as-is (`https://login.microsoftonline.com/{tenant}/v2.0`); the tool converts it to the real token endpoint `.../oauth2/v2.0/token` automatically. |
| Client ID | Shown in CargoWise after you upload your CSR and generate the certificate |
| Scope | Pre-filled with the template `{clientid}/.default` - it auto-follows the Client ID as you type (and `{clientid}` is resolved again at request time). Clear it to omit the scope entirely |
| Certificate | The certificate CargoWise generated from **your** CSR, together with **your** private key: either a `.pem`/`.cer` certificate + `.key`/`.pem` private key (PKCS#1 or PKCS#8, encrypted PKCS#8 supported), or a PFX/P12. For the PEM pair you can **paste the PEM text directly** into the fields (auto-detected), load the files from your machine via *Load file...*, or point at paths on the server. PFX is path-based - the backend reads local files, so use paths visible to the machine running the tool |

The auth flow is the standard OAuth 2.0 client-credentials grant with certificate assertion:
a short-lived RS256-signed JWT (`x5t` = SHA-1 thumbprint of your certificate) is POSTed to the
token endpoint, the returned bearer token is attached to the XML POST, and response gzip is
handled automatically. Tokens are cached and reused until shortly before expiry.

## Getting a certificate and key

The tool needs a client certificate issued by CargoWise plus the matching RSA private key
(the client assertion is signed RS256, so the key must be RSA; 2048 bits is the usual choice).
One CSR round-trip produces both.

### Option A - generate the key and CSR in the tool (no openssl needed)

Click **Generate key + CSR...** next to the certificate fields. Enter your **EnterpriseID**
(e.g. `XYZ`) - the CSR Common Name follows the CargoWise convention
`{EnterpriseID}-eAdaptorNext` - and press **Generate CSR + Key**. Download (or copy) the two
files: the `.csr` for CargoWise and the `.key` - your private key, which never leaves your
machine. The dialog also shows the equivalent openssl command if you prefer the command line.

### Option B - openssl on the command line

If `openssl` is not found: it ships with **Git for Windows**
(`C:\Program Files\Git\usr\bin\openssl.exe` - and inside Git Bash it is simply `openssl`).
Otherwise install a standalone "Win32/Win64 OpenSSL" build; on Linux and macOS it is normally
preinstalled.

```bash
openssl req -newkey rsa:2048 -nodes -keyout eadaptor.key -out eadaptor.csr -subj "//CN=XYZ-eAdaptorNext"
```

- Drop the `-subj` argument if you prefer to answer the prompts interactively.
- The `-subj` value uses a double leading slash so it survives Git Bash's path mangling;
  components are separated by `/`, e.g. `"//CN=XYZ-eAdaptorNext/O=My Company/C=GB"`.
- On Windows PowerShell, call the openssl shipped with Git and use a single leading slash instead:
  `& "C:\Program Files\Git\usr\bin\openssl.exe" req -newkey rsa:2048 -nodes -keyout eadaptor.key -out eadaptor.csr -subj "/CN=XYZ-eAdaptorNext"`
- Leave out `-nodes` if you want an encrypted key (set a password when prompted) - then fill in
  the *Key password* field in the tool.

### Upload the CSR to CargoWise

In CargoWise: *Maintain > EDI Messaging > EDI Client Details*, **Inbound** tab, upload `eadaptor.csr`,
then click **Generate Certificate**.

### Save the issued certificate

CargoWise returns a PEM certificate (`-----BEGIN CERTIFICATE-----`); save it as a text file such as
`eadaptor.pem`, or copy the text for pasting directly into the tool. Note the **Authority URL**,
**Client ID** and **Scope** shown on the same page.

### Verify the pair matches (optional)

```bash
openssl x509 -noout -modulus -in eadaptor.pem | openssl md5
openssl rsa  -noout -modulus -in eadaptor.key | openssl md5
```

The two hashes must be identical.

Back in the tool: paste the certificate and the key into *Certificate* and *Private key* (or load
the files), fill in the token endpoint, client ID and scope, and press **Test authentication**.
When it comes back green, send something real.

## The response

- HTTP status + timing + response headers.
- The body is parsed for a `UniversalResponse` envelope: **Status** (OK / ERR) and
  **ProcessingLog** are surfaced as badges, and a failed upload looks like:

```
HTTP/1.1 200 OK
<UniversalResponse ...>
  <Status>ERR</Status>
  <ProcessingLog>Recipient XXXYYYZZZ could not be resolved</ProcessingLog>
</UniversalResponse>
```

## API (scriptable)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/settings` | current settings |
| POST | `/api/test` | validate authentication only |
| POST | `/api/send` | `{ settings, body }` - send XML, returns the full result |
| POST | `/api/format` | pretty-print XML server-side |
| GET | `/api/sample` | a minimal sample `UniversalInterchange` |

## Security notes

This is a **local developer tool** with no authentication. Keep it bound to localhost and never
expose it to untrusted networks. The tool **stores nothing**: everything you type lives only in the
browser page (a refresh clears it) and credentials exist in server memory just long enough for one
request. Protect the `.key`/`.pem` files you paste or load yourself, and use a CargoWise **test**
environment for integration work.

## Building

```
dotnet build src/EadaptorNextPing/EadaptorNextPing.csproj
```

Any .NET 8 SDK includes the ASP.NET Core runtime used here. Publishing:

```
dotnet publish src/EadaptorNextPing -c Release -r win-x64 --self-contained true
```

## Contributing

Keep it small: one page, one purpose, zero client-side frameworks, zero NuGet packages.
UI text and comments in English. PRs welcome.

## Acknowledgements

eAdaptor and CargoWise are products of WiseTech Global. This project is an independent community
tool and is not affiliated with or endorsed by WiseTech Global.

## License

[MIT](LICENSE) - (c) 2026 eAdaptor Next Ping Contributors
