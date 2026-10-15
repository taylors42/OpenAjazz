#!/usr/bin/env bash
# Installs access rules for only the two legacy AJ199 USB IDs.
set -euo pipefail
if [[ $(id -u) != 0 ]]; then
  echo 'Execute com pkexec ou sudo para instalar a regra de acesso ao AJ199.' >&2
  exit 1
fi
install -d -m 0755 /etc/udev/rules.d
cat > /etc/udev/rules.d/70-ajazz-aj199.rules <<'RULES'
# AJ199 legacy devices: active local session only.
SUBSYSTEM=="hidraw", KERNEL=="hidraw*", ATTRS{idVendor}=="25a7", ATTRS{idProduct}=="fa7b", TAG+="uaccess"
SUBSYSTEM=="hidraw", KERNEL=="hidraw*", ATTRS{idVendor}=="25a7", ATTRS{idProduct}=="fa7c", TAG+="uaccess"
RULES
chmod 0644 /etc/udev/rules.d/70-ajazz-aj199.rules
udevadm control --reload-rules
for device in /sys/class/hidraw/hidraw*; do
  if [[ -f "$device/device/uevent" ]] && /usr/bin/grep -Eq '^HID_ID=0003:000025A7:0000FA7[BC]$' "$device/device/uevent"; then
    udevadm trigger --action=add "$device"
  fi
done
udevadm settle --timeout=10
printf 'Regra do AJ199 instalada. Reconecte o receptor se o acesso ainda não estiver disponível.\n'
