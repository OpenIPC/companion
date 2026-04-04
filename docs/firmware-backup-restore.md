# Firmware Backup and Restore

> **WARNING: Restoring firmware is a destructive, irreversible operation. Flashing incorrect data or losing power mid-flash can permanently brick your device. Proceed only if you understand the risks and have a verified backup.**

## What the backup contains

When you create a backup from the Firmware tab, the app:

1. Kills the streamer and other large processes to free RAM
2. Dumps every MTD block device found under `/dev/mtdblock*` using `dd`
3. Generates MD5 checksums for every `.bin` file
4. Packages everything into a `.tar.gz` archive and downloads it to your machine

The archive contains:
```
backup-<chiptype>-<timestamp>/
  mtdblock0.bin   # bootloader (U-Boot)
  mtdblock1.bin   # bootloader environment
  mtdblock2.bin   # kernel
  mtdblock3.bin   # rootfs
  mtdblock4.bin   # rootfs_data (user settings, overlays)
  md5sums.txt     # MD5 checksums for verification
```

Partition names and count vary by SoC. The `rootfs_data` partition contains your runtime configuration — restoring it will overwrite any settings changed after the backup was made.

---

## Restoring with Companion (recommended)

Requirements: the device must be reachable over SSH (i.e., it still boots and is on the network).

1. Open the **Firmware** tab and expand **Backup / Restore**
2. Check **"I accept the risk of permanently bricking this device"**
3. Click **Restore from Backup**
4. Select your `.tar.gz` backup archive
5. Confirm the warning dialog
6. The app will:
   - Extract and verify MD5 checksums locally
   - Read `/proc/mtd` from the device to confirm partition layout
   - Upload and `flashcp` each partition in order
   - Reboot the device when done

If checksum verification fails, the restore is aborted before any data is written to the device.

---

## Restoring manually over SSH

If you cannot use the Companion app but the device is still SSH-accessible:

```bash
# 1. Extract the backup archive
tar -xzf backup-<chiptype>-<timestamp>.tar.gz
cd backup-<chiptype>-<timestamp>

# 2. Verify checksums
md5sum -c md5sums.txt

# 3. Copy partitions to the device
scp mtdblock*.bin root@<device-ip>:/tmp/

# 4. SSH into the device and flash each partition
ssh root@<device-ip>

flashcp -v /tmp/mtdblock0.bin /dev/mtd0   # bootloader
flashcp -v /tmp/mtdblock1.bin /dev/mtd1   # env
flashcp -v /tmp/mtdblock2.bin /dev/mtd2   # kernel
flashcp -v /tmp/mtdblock3.bin /dev/mtd3   # rootfs
flashcp -v /tmp/mtdblock4.bin /dev/mtd4   # rootfs_data

reboot
```

`flashcp` erases and writes atomically — do not use `dd` to write back to MTD partitions as it does not erase first. Adjust partition numbers to match your device's actual `/proc/mtd` layout.

To flash only kernel and rootfs (leaving bootloader and settings intact):

```bash
flashcp -v /tmp/mtdblock2.bin /dev/mtd2
flashcp -v /tmp/mtdblock3.bin /dev/mtd3
reboot
```

---

## Restoring via U-Boot (device won't boot)

If the kernel or rootfs is corrupted and the device cannot reach Linux, recovery through U-Boot over TFTP is possible — provided the bootloader itself is intact.

> **This method requires device-specific knowledge. Flash memory addresses, load addresses, partition sizes, and erase block sizes all depend on your exact SoC and board. Using incorrect values will cause further damage. Do not proceed without consulting the documentation or U-Boot output for your specific device.**

### Requirements
- Serial console access (UART adapter connected to the device's TX/RX/GND pads)
- A TFTP server running on your machine (e.g. `tftpd`, `tftp-hpa`, or Tftpd64 on Windows)
- The correct flash addresses for your specific SoC — read these from the U-Boot boot log or from `/proc/mtd` on a working device of the same model

### General approach

1. Connect serial console and power on. Interrupt autoboot to reach the U-Boot prompt.
2. Configure networking:
   ```
   setenv ipaddr <device-ip>
   setenv serverip <your-tftp-server-ip>
   ```
3. Load and flash the kernel and rootfs from your TFTP server using the addresses and sizes specific to your device.
4. Run `reset` to reboot.

The exact `sf erase`, `sf write`, and RAM load addresses vary per device. Consult the [OpenIPC supported hardware page](https://openipc.org/supported-hardware/featured) for your specific SoC, and cross-reference with the U-Boot startup output for your board before attempting this.

### If the bootloader (mtd0) is also corrupted

A corrupted bootloader means the device produces no serial output and cannot be recovered via software. Recovery requires a hardware flash programmer (e.g. CH341A) connected directly to the SPI NOR chip.

---

## Restoring from SD card (SigmaStar, Ingenic, and some others)

Some processors — including SigmaStar and Ingenic — initialise the SD card in the bootloader, which allows recovery without serial access. At startup, U-Boot searches the SD card for a command script (`boot.scr`); if found, the commands in that file are executed before Linux boots.

### Requirements
- A FAT-formatted SD card
- The kernel and rootfs `.bin` files you want to flash
- The `mkimage` tool (part of U-Boot tools) to create the script, **or** a pre-built `boot.scr` from a trusted source

### Steps

1. Create a U-Boot script that tells the bootloader to flash from the SD card:

   ```bash
   echo -e "setenv updatetool fatload mmc 0\nrun uknor\nrun urnor" > bootcmd.txt
   mkimage -A arm -T script -d bootcmd.txt boot.scr
   ```

2. Copy `boot.scr`, the kernel binary, and the rootfs binary to the root of the FAT SD card.

3. Insert the SD card into the device and power it on. The bootloader will detect `boot.scr`, execute the flash commands, and reboot into the restored firmware.

> The script above uses the standard OpenIPC U-Boot environment variables (`uknor`, `urnor`). These are pre-defined in the OpenIPC bootloader and handle the correct addresses for your device automatically. This method is not guaranteed to work on stock/vendor firmware where these variables may not exist.

For more detail see the [OpenIPC wiki](https://github.com/OpenIPC/wiki/blob/master/en/faq.md#how-to-restore-kernel-or-root-file-system-from-sd-card-).

---

## Partition reference

| Partition | Typical device node | Contents | Safe to restore over SSH? |
|-----------|-------------------|----------|--------------------------|
| boot | `/dev/mtd0` | U-Boot bootloader | Yes, if device stays powered |
| env | `/dev/mtd1` | U-Boot environment variables | Yes |
| kernel | `/dev/mtd2` | Linux kernel (uImage) | Yes |
| rootfs | `/dev/mtd3` | Root filesystem (SquashFS) | Yes |
| rootfs_data | `/dev/mtd4` | Overlay/user config | Yes — overwrites current settings |

Partition layout and numbering varies by SoC and vendor. Always verify against `/proc/mtd` on your specific device before flashing.
