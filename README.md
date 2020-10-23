# spicam: Simple Pi Cam

A bare-bones Raspberry Pi security camera application with motion detection, video clip and snapshot capture, and email notification support.

| :warning: Currently depends on a pre-release (v0.7) MMALSharp library using locally-built DLLs. |
| --- |

## Features
* Configurable motion detection ([MMALSharp wiki](https://github.com/techyian/MMALSharp/wiki/CCTV-and-Motion-Detection))
* h.264 video clips of motion detection events
* JPEG snapshots at the start of motion detection
* Motion detection mask
* Streaming video (disables motion detection)
* Streaming motion analysis (high-stress, Pi 4 suggested)
* Email notifications with optional snapshot attachments

## Requirements
* .NET Core 3.1 runtime

## Recommendations
* Local unsecured SMTP port 25 relay to a secure mail server
* Ramdisk for temporary local video buffering and storage
* External storage (HDD, SDD, NAS) for video and snapshot storage

## TODO List
* Document app configuration (appsettings.json)
* Document setup of requirements and mail relay
* Document usage as systemd service and provide scripts
* Add rolling-file logging
* Transcode to MP4 when moving h.264 to external storage
