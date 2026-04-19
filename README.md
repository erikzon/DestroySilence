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