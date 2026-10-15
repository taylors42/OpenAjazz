# Protocolo parcial do AJ199 legado

Especificação obtida por análise estática do configurador Windows, leitura de descritores HID e transações no receptor local. O executável Windows não foi executado. Os instaladores, arquivos extraídos, strings e desmontagens completas não são redistribuídos neste repositório.

Esta especificação descreve somente a variante observada e não é uma especificação oficial da AJAZZ. Consulte [a verificação física](POC.md) antes de interpretar leitura posterior como comprovação de todos os efeitos.

## Identificação e origem

- Instalador estudado: `AJAZZ AJ199 Setup v1.0 20231205.exe`.
- SHA-256 do instalador: `0c8b3a0c4d31922cb0fb9daa14af58211a2939a2afc8113b041de3ecbdc7852c`.
- Aplicação analisada: `OemDrv.exe`, PE32 x86, SHA-256 `ac9ca7cf9e072322589324f3027a65b681ccee8b44bf7c74b288627bf9d58846`.
- Receptor observado: `25A7:FA7C`, nome USB Compx 2.4G Wireless Receiver.
- Identidade adicional esperada: CID `0x40`, MID `0x01`.
- `25A7:FA7B` está previsto como cabo na descoberta, mas não foi validado fisicamente.

VID/PID sozinho não basta para autorizar escrita. A aplicação também verifica a identidade e o canal de configuração. Os endereços de rotinas abaixo são virtuais do PE com base `0x00400000`.

## Transporte

Na interface USB 1, a coleção vendor usage page `0xFF02`, usage `0x0002` expõe Feature Report `0x08` e Input Report `0x09`, ambos com 16 bytes de payload, além do ID. A fixture `receiver-interface1.bin` registra o descritor HID observado.

Usar HIDAPI/hidraw para enviar Feature Reports de 17 bytes e ler respostas de 17 bytes. Outros reports de entrada não são interpretados como resposta de configuração nem registrados. Report ID `0x09` não deve ser confundido com opcode `0x09`.

| Byte | Requisição | Resposta |
| --- | --- | --- |
| 0 | Report ID `0x08` | Report ID `0x09` |
| 1 | Opcode | Opcode correlacionado |
| 2 | Reservado, zero | Status; zero indica sucesso observado |
| 3–4 | Endereço, byte alto primeiro | Mesmo endereço |
| 5 | Quantidade | Quantidade de dados |
| 6–15 | Até dez bytes de dados | Dados |
| 16 | Checksum | Checksum |

Checksum: `(0x55 - soma(bytes[0..15])) & 0xFF`. A soma de todos os 17 bytes deve ser `0x55 mod 256`. Validar opcode, endereço, tamanho esperado e status antes de aceitar uma resposta.

## Comandos utilizados

| Opcode | Uso observado |
| --- | --- |
| `0x01` | Handshake com desafio de quatro bytes; CID em resposta[10], MID em [11] |
| `0x02` | Sessão de aplicativo ativa/inativa; tamanho 1, valor 1/0 |
| `0x03` | Estado de conexão; observado 0 sem resposta do mouse e 1 com perfil acessível |
| `0x04` | Energia indicada pelo receptor em resposta[6]; interpretação completa da bateria pendente |
| `0x07` | Escrever bloco de memória |
| `0x08` | Ler bloco de memória |
| `0x0E` | Consultar perfil atual; somente perfil 0 suportado nesta PoC |

Uma resposta de identidade capturada é `09 01 00 00 00 08 6E 66 BE 05 40 01 00 00 00 00 6B`. Os vetores de requisição estão em `LegacyProtocol.cs` e nos testes.

Não enviar opcode `0x09` como commit: seu significado não foi confirmado neste firmware e referências de famílias relacionadas o associam a restauração. A alteração de DPI confirmada não precisou desse comando.

## Mapa usado pela PoC

A aplicação lê os primeiros 84 bytes do perfil. Cada registro usado tem checksum próprio: a soma de seus bytes é `0x55 mod 256`, independentemente do checksum do transporte.

| Endereço | Formato | Significado |
| --- | --- | --- |
| `0x00..01` | Valor, checksum | Hz: 1=1000, 2=500, 4=250, 8=125 |
| `0x02..03` | Valor, checksum | Quantidade de estágios; preservada |
| `0x04..05` | Valor, checksum | Índice ativo a partir de zero; preservado |
| `0x0C + índice × 4` | X baixo, Y baixo, flags, checksum | DPI, oito posições |
| `0x2C + índice × 4` | R, G, B, checksum | Cor associada ao estágio |
| `0x4C..53` | Não alterado | Parâmetros de iluminação ainda fora do escopo |

Não extrapolar esse mapa para outras variantes. O receptor observado combina quantidade 1 com índice ativo 1; a PoC valida o índice contra as oito posições, sem modificar esses dois registros ou inferir que índice=quantidade−1.

### DPI do PAW3395

```text
Xcode = Xlo | ((flags & 0x0C) << 6)
Ycode = Ylo | ((flags & 0xC0) << 2)
Xdpi = (Xcode + 1) * 50 * (bit 0 ativo ? 2 : 1)
Ydpi = (Ycode + 1) * 50 * (bit 4 ativo ? 2 : 1)
```

O encoder de 100 a 26000 DPI usa `code = dpi / 50 - 1`, bits altos em flags e multiplicadores zerados. O controle único grava X e Y iguais.

### Rotinas que fundamentam o layout

| Endereço | Interpretação |
| --- | --- |
| `0x40F660` | Leitura do perfil |
| `0x40F9D7` / `0x40F9E7` | Carrega índice bruto e salva em `g_nCurDpiLevel` |
| `0x404810` | Usa o índice diretamente na seleção entre oito controles |
| `0x411885` | Grava o mesmo índice em `0x04` |
| `0x40CE10` / `0x40CA40` | Conversão entre DPI e código do sensor |
| `0x4513B0` | Gravação dos registros de DPI |
| `0x451290` | Gravação de cor por estágio |
| `0x451780` | Gravação da taxa de resposta |
| `0x450DC0` / `0x451120` | Escrita/leitura de blocos |

A interpretação anterior do índice como base 1 estava errada. A regressão e sua verificação estão documentadas em [POC.md](POC.md).

## Limites e reprodução

Os testes reproduzem framing e conversões usando fixtures pequenas; não exigem instalador Windows. Para examinar o dispositivo próprio sem enviar comandos, use `python3 scripts/inspect_linux.py`. Para consultas reais use a CLI conforme o README, com a regra udev instalada e o mouse acordado.

Continuam fora do protocolo implementado: macros, remapeamento, LOD, debounce, motion sync, mais perfis, atualização de firmware, reset e semântica completa da bateria. Persistência, cabo e mudanças de taxa precisam de testes próprios.

As fontes de consulta, dependências e respectivas atribuições estão em [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md). Materiais de pesquisa locais não são necessários para compilar ou executar a aplicação.
