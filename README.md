# Destroy Silence

A Blazor WebAssembly application that automatically removes silence from uploaded videos using FFmpeg in the browser.

## Features

- Upload video files
- Automatic silence detection and removal
- Client-side processing (no server upload)
- Download processed video

## Prerequisites

- .NET 10.0 SDK
- Modern web browser with SharedArrayBuffer support (Chrome, Firefox, etc.)

## Running the Application

1. Clone or open the project
2. Run `dotnet run` or `dotnet watch`
3. Open the browser to the displayed URL
4. Select a video file and click "Process Video"

## How it works

The application uses FFmpeg.wasm to:
1. Analyze the video for silence periods
2. Create segments of non-silent audio/video
3. Concatenate the segments into a new video

## Notes

- Processing happens entirely in the browser
- Large videos may take time to process
 - Ensure CORS headers are set for local development if needed

## Firebase Hosting

The repository includes a `firebase.json` that configures Firebase Hosting to serve the published Blazor output and inject the required cross-origin headers for FFmpeg/WebAssembly to work.

- **Public folder**: `bin/Release/net10.0/publish/wwwroot`
- **Injected headers**:
	- `Cross-Origin-Embedder-Policy: require-corp`
	- `Cross-Origin-Opener-Policy: same-origin`

Build and deploy:

```bash
dotnet publish -c Release -o bin/Release/net10.0/publish
npm install -g firebase-tools
firebase login
firebase init hosting
# When prompted, set the public directory to:
bin/Release/net10.0/publish/wwwroot
# (You can keep the default rewrites; firebase.json already includes one.)
firebase deploy --only hosting
```

Quick local test (use the Firebase CLI/emulator):

```bash
firebase emulators:start --only hosting
# or:
firebase serve --public bin/Release/net10.0/publish/wwwroot --port 5000
```

Running it locally need some headers from the release folder
```
dotnet serve -d ./bin/Release/net10.0/publish/wwwroot -p 8000 -h "Cross-Origin-Embedder-Policy: require-corp" -h "Cross-Origin-Opener-Policy: same-origin"
```