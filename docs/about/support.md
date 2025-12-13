# Support

Need help with VUWare? Here are the resources available to you.

## Documentation

This documentation site is your primary resource for help:

- **[Installation Guide](../getting-started/installation.md)** - Step-by-step installation
- **[First Launch](../getting-started/first-launch.md)** - Initial setup wizard
- **[Configuration](../getting-started/configuration.md)** - Dial and sensor setup
- **[Common Use Cases](../user-guide/use-cases.md)** - Example configurations
- **[Settings Reference](../user-guide/settings.md)** - Detailed settings documentation
- **[Troubleshooting](../user-guide/troubleshooting.md)** - Solutions to common issues

!!! tip "Start Here"
    The [Troubleshooting Guide](../user-guide/troubleshooting.md) covers the most common issues and their solutions.

## GitHub Issues

For bug reports, feature requests, and technical questions:

**[VUWare GitHub Issues](https://github.com/uweinside/VUWare/issues)**

### Before Opening an Issue

1. **Search existing issues** - Your question may already be answered
2. **Check the documentation** - Especially the troubleshooting guide
3. **Try basic troubleshooting** - Restart, reinstall, verify prerequisites

### Reporting Bugs

When reporting a bug, please include:

**System Information**:
- VUWare version (check installer or About dialog)
- Windows version (run `winver` to check)
- HWInfo64 version
- .NET Runtime version (`dotnet --list-runtimes`)

**Hardware Details**:
- VU1 Hub model/manufacturer
- Number of dials connected
- PC hardware (CPU, GPU, motherboard if relevant)

**Steps to Reproduce**:
1. Describe exactly what you were doing
2. What you expected to happen
3. What actually happened
4. Include exact error messages

**Additional Files** (if applicable):
- Configuration file: `C:\Program Files\VUWare\Config\dials-config.json`
- Screenshots of error messages
- Windows Event Viewer logs

**Example Issue Title**:
```
[Bug] Dials not updating after HWInfo64 restart on Windows 11
```

### Requesting Features

When requesting a new feature:

**Describe the feature**:
- What functionality would you like added?
- Why would this be useful?
- How should it work?

**Use cases**:
- Provide specific examples of how you'd use it
- Explain what problem it solves

**Example Issue Title**:
```
[Feature Request] Support for more than 4 dials
```

### Asking Questions

For general questions about usage:

**What to include**:
- What you're trying to accomplish
- What you've tried already
- What specific part you need help with

**Example Issue Title**:
```
[Question] How to monitor NVMe drive temperature?
```

## Prerequisites for Support

Before requesting support, ensure:

### Hardware

- [x] VU1 Gauge Hub is connected and powered
- [x] VU1 dials are connected to hub via I2C
- [x] Hub appears in Windows Device Manager
- [x] Dials power on (LED indicators light up)

### Software

- [x] Windows 10 version 1809 or later (64-bit)
- [x] .NET 8.0 Runtime is installed
- [x] HWInfo64 is installed and running
- [x] "Shared Memory Support" is enabled in HWInfo64
- [x] HWInfo64 was restarted after enabling shared memory

### VUWare

- [x] VUWare is latest version from releases page
- [x] Configuration file exists and is not corrupted
- [x] You've tried basic troubleshooting (restart, reinstall)

## Self-Help Resources

### Common Issues Quick Links

- **[Installation Problems](../user-guide/troubleshooting.md#installation-issues)**
- **[Hub Not Detected](../user-guide/troubleshooting.md#vu1-hub-not-detected)**
- **[Dials Not Found](../user-guide/troubleshooting.md#dials-not-detected)**
- **[HWInfo64 Connection](../user-guide/troubleshooting.md#sensors-not-available)**
- **[Dials Not Updating](../user-guide/troubleshooting.md#dials-not-updating)**
- **[Colors Not Changing](../user-guide/troubleshooting.md#colors-not-changing)**
- **[Configuration Not Saving](../user-guide/troubleshooting.md#configuration-not-saving)**

### HWInfo64 Help

For HWInfo64-specific issues:

- **HWInfo64 Documentation**: [https://www.hwinfo.com/forum/](https://www.hwinfo.com/forum/)
- **HWInfo64 Support**: Use their official support channels
- **Shared Memory**: VUWare only requires "Shared Memory Support" to be enabled

### VU1 Hardware Help

For VU1 Gauge Hub or dial hardware issues:

- Contact your VU1 hardware manufacturer
- Check I2C connections and power
- Verify dial firmware is up to date

**Serial Driver Installation**:

Windows should automatically recognize and install the serial COM port driver. If it doesn't, you can download and manually install the drivers from:

- **FTDI VCP Drivers**: [https://ftdichip.com/drivers/vcp-drivers/](https://ftdichip.com/drivers/vcp-drivers/)

After installation:
1. Restart your computer
2. Reconnect the VU1 Hub
3. Check Device Manager to verify the COM port appears

## Community Support

### Discussions

Check the GitHub Discussions tab for:
- General questions
- Configuration advice
- Tips and tricks
- Sharing setups

**[GitHub Discussions](https://github.com/uweinside/VUWare/discussions)** (if enabled)

### Social Media

Stay updated with VUWare development:
- Watch the GitHub repository for updates
- Star the project if you find it useful
- Share your configurations and setups

## Response Times

VUWare is maintained as an open-source project:

- **Bug fixes**: Critical bugs are prioritized
- **Feature requests**: Evaluated based on community interest
- **Questions**: Answered as time permits

!!! info "Open Source Project"
    VUWare is maintained by volunteers. Response times may vary. We appreciate your patience and understanding.

## Contributing

Want to help improve VUWare?

### Ways to Contribute

**Code Contributions**:
- Fork the repository
- Submit pull requests for bug fixes or features
- Follow coding standards and conventions

**Documentation**:
- Improve this documentation
- Add examples and use cases
- Fix typos or clarify instructions

**Testing**:
- Test pre-release versions
- Report bugs with detailed reproduction steps
- Verify bug fixes work on your system

**Community Support**:
- Help answer questions from other users
- Share your configurations and tips
- Provide feedback on proposed features

See the repository [CONTRIBUTING.md](https://github.com/uweinside/VUWare/blob/main/CONTRIBUTING.md) (if it exists) for detailed contribution guidelines.

## Security Issues

For security vulnerabilities, please:

1. **Do not** open a public GitHub issue
2. Contact via email or GitHub security advisory
3. Provide detailed description of the vulnerability
4. Allow reasonable time for a fix before public disclosure

## Commercial Support

VUWare is free and open-source software. Commercial support is not officially offered, but:

- Fork the repository and customize for your needs
- Hire developers familiar with .NET/C# for custom modifications
- The MIT License allows commercial use and modification

## Contact

For non-support inquiries:

- **Project Home**: [https://github.com/uweinside/VUWare](https://github.com/uweinside/VUWare)
- **Issues**: [https://github.com/uweinside/VUWare/issues](https://github.com/uweinside/VUWare/issues)
- **Author**: Uwe Baumann (via GitHub)

## Credits

VUWare is developed and maintained by **Uwe Baumann**.

Special thanks to:
- HWInfo64 developers for the excellent monitoring tool
- VU1 hardware creators for the beautiful analog gauges
- Open-source community for libraries and tools
- Users who report issues and contribute improvements

## Acknowledgments

VUWare uses:
- .NET 8.0 Runtime (Microsoft)
- Material Design theme for documentation (Google)
- MkDocs for documentation generation

Thank you for using VUWare!
