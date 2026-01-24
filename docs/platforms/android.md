# Android

This is still an alpha release. When using a tunnel on Radxa, the app can hang. This is under investigation and may be related to a command waiting on a broken connection.

## Accessing app data
```bash
adb shell
run-as org.openipc.Companion
ls /data/user/0/org.openipc.Companion
```

or 

```bash
adb shell run-as org.openipc.Companion ls /data/user/0/org.openipc.Companion
```

## Accessing binaries
```bash
adb shell
run-as org.openipc.Companion
ls -R /data/data/org.openipc.Companion/files
```

or 

```bash
adb shell run-as org.openipc.Companion ls /data/data/org.openipc.Companion/files
```
