# VUWare.Console - Interactive Dial Controller

A professional command-line interface for controlling Streacom VU1 dials via the VU1 Gauge Hub. Features comprehensive logging, real-time status display, and guided automated testing.

## Features

- **Auto-Detection**: Automatically finds and connects to VU1 Gauge Hub via USB
- **Interactive Commands**: 12+ commands for complete dial control
- **Dial Discovery**: Automatic I2C bus scanning and device provisioning
- **Position Control**: Set dial positions 0-100% with smooth animations
- **Backlight Control**: 11 predefined colors (Red, Green, Blue, Yellow, Cyan, Magenta, Orange, Purple, Pink, White, Off)
- **Display Management**: Upload custom 1-bit images (200x144, 3600 bytes) to e-paper displays
- **Automated Testing**: Single-command dial test suite with auto-setup
- **Comprehensive Logging**: Timestamped operation logs with performance metrics
- **Professional Output**: Color-coded console output with visual progress indicators

## Quick Start

### Prerequisites

- .NET 8.0 or later
- VU1 Gauge Hub connected via USB
- One or more VU1 dials connected to the hub

### Installation & Setup

```bash
cd C:\Repos\VUWare

dotnet build VUWare.sln

dotnet run --project VUWare.Console
```

### Basic Usage

```
> connect
? Connected to VU1 Hub!
> init
? Initialized! Found 2 dial(s).
> dials
[List of dials]
> set <uid> 75
? Dial set to 75%
> color <uid> red
? Backlight set to Red
```

## Display Image Format

| Property | Value |
|----------|-------|
| Resolution | 200 x 144 pixels |
| Color Depth | 1-bit (black/white threshold) |
| Packed Size | 3600 bytes ((200*144)/8) |
| Packing | Vertical: 8 pixels per byte (MSB = top) |
| Threshold | Gray > 127 ? bit 1 (light), ?127 ? bit 0 (dark) |
| Supported Inputs | PNG, BMP, JPEG (auto-scaled & converted) |

Images are automatically:
1. Loaded (PNG/BMP/JPEG)
2. Aspect-scaled to fit 200x144 (white letterbox if needed)
3. Converted to grayscale (luminosity 0.299R + 0.587G + 0.114B)
4. Thresholded & packed column-by-column

Upload example:
```bash
> image <uid> ./etc/image_pack/cpu-temp.png
```

## Commands Reference

| Command | Description |
|---------|-------------|
| `connect` | Auto-detect and connect to hub |
| `connect <port>` | Connect to specific COM port |
| `disconnect` | Disconnect from hub |
| `init` | Discover and initialize dials |
| `status` | Show connection/initialization status |
| `dials` | List all dials |
| `dial <uid>` | Show detailed info for one dial |
| `set <uid> <percent>` | Set dial position (0-100) |
| `color <uid> <name>` | Set backlight color |
| `colors` | List available colors |
| `image <uid> <file>` | Upload/convert image to display |
| `test` | Automated dial test suite |
| `help` | Show help info |
| `exit` | Exit console |

## Performance Characteristics

| Operation | Typical Time |
|-----------|--------------|
| Auto-detect | 1-3 s |
| Discovery (1 dial) | 1-2 s |
| Set position | ~50-100 ms |
| Set color | ~50-100 ms |
| Image upload (3600 bytes) | 2-3 s |

## Limits

- **Max Dials**: 100
- **Image Size**: 3600 bytes (enforced)
- **Chunk Size**: 1000 bytes (auto-split)
- **Formats**: PNG, BMP, JPEG input -> packed 1-bit
- **Position Range**: 0-100%
- **Color Channels**: 0-100% each (RGBW)

## Troubleshooting

### Image Upload Fails
**Checklist:**
- Source converts to 200x144 (aspect preserved)
- Packed size equals 3600 bytes
- Dial is initialized (`init` run)
- Try a blank image: `image <uid> ./etc/image_pack/blank.png`

### Dial Not Responding
- Verify connection (`status`)
- Re-run `init`
- Check USB cable & power

### Slow Operations
- Check USB hub quality
- Keep I2C cables short

## Example Workflow

```bash
> connect
> init
> dials
> set <uid> 50
> color <uid> cyan
> image <uid> ./etc/image_pack/gpu-temp.png
```

## Reference Images
All reference images in `etc/image_pack/` are already 200x144 and suitable for direct upload.

## Tips
1. Always run `connect` then `init` first.
2. Use `dials` to copy UIDs.
3. Use `test` for a fast health check.
4. Queue multiple images by calling `image` sequentially.

## FAQ

**Q: Do I need to pre-convert images?**
A: No, conversion happens automatically.

**Q: Why 3600 bytes instead of 5000?**
A: The panel’s physical resolution is 200x144; earlier 200x200 assumptions were incorrect.

**Q: Can I use color PNGs?**
A: Yes, they are converted to grayscale then thresholded.

## Related Docs
- `VUWare.Lib/README.md` – Library API
- `VUWare.Lib/doc/SERIAL_PROTOCOL.md` – Protocol details

## License
[Your License Here]

## Support
GitHub Issues & Discussions

