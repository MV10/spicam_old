# spicam: Simple Pi Cam

A bare-bones Raspberry Pi security camera application with motion detection, video clip and snapshot capture, and email notification support.

| :warning: Currently depends on a pre-release (v0.7) MMALSharp library using locally-built DLLs. |
| --- |

| :warning: Very alpha-quality code, don't bother using it yet... |
| --- |

## Features
* Configurable motion detection ([MMALSharp wiki](https://github.com/techyian/MMALSharp/wiki/CCTV-and-Motion-Detection))
* h.264 video clips of motion detection events
* JPEG snapshots at the start of motion detection
* Email notifications with optional snapshot attachments
* Optional motion detection mask
* Coming soon: Streaming video (disables motion detection)
* Coming soon: Streaming motion analysis (high-stress, Pi 4 suggested)

## Requirements
* .NET Core 3.1 runtime

## Recommendations
* Local unsecured SMTP port 25 relay to a secure mail server
* Ramdisk for temporary local video buffering and storage
* External storage (HDD, SDD, NAS) for video and snapshot storage

## TODO List
* Implement non-motion-detection switches / features
* Document app configuration (appsettings.json)
* Document setup of requirements and mail relay
* Document usage as systemd service and provide scripts
* Add rolling-file logging
* Transcode to MP4 when moving h.264 to external storage
