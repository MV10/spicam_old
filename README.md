# spicam: Simple Pi Cam

A bare-bones Raspberry Pi security camera application with motion detection, video clip and snapshot capture, and email notification support.

## ARCHIVED

The `spicam` repo was renamed `spicam_old` and archived when Broadcom changed the proprietary GPU code, effectively preventing much of
what we were doing via MMAL. So much for the Pi Foundation constantly rah-rah-ing about open source, eh? While this would have been a very
fun project to complete and use, I gave up on this angle. The new `spicam` will simply monitor modern Amcrest IP cameras (specifically) for
event notifications and route the relevant clips to safe storage on a NAS. Not nearly as interesting, but it was probably inevitable that
onboard processing would become this cheap and commonplace.

## Original README:

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
