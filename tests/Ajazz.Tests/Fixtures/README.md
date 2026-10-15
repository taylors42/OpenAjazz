# Origem das fixtures

`receiver-interface1.bin` é uma cópia do descritor HID de 189 bytes lido no sysfs em 15/10/2026, receptor Compx `25A7:FA7C`, interface USB 1. Não contém eventos de entrada ou configuração pessoal.

Os vetores hexadecimais em `ProtocolTests.cs` são sintéticos, derivados da desmontagem do OemDrv identificado no estudo. Eles verificam framing, endianness e checksum. Não são capturas USB nem comprovam que o firmware responde aos comandos.

`aj199-profile.bin` contém os 84 bytes do perfil real do AJ199 `40:01`, receptor `25A7:FA7C`, lidos em 15/10/2026: 1600 DPI, 1000 Hz, verde no estágio ativo de índice 1 (segundo registro) e quantidade de estágios 1. O índice ativo é armazenado a partir de zero; quantidade e índice são preservados independentemente, conforme observado no receptor. Não contém macros ou eventos de entrada.
