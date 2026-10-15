# Dependências e referências

O código próprio do OpenAjazz é distribuído sob a [licença MIT](LICENSE). Dependências e fontes de consulta mantêm suas licenças e autorias próprias. Este repositório publica código-fonte, não um pacote de bibliotecas nativas ou um executável self-contained.

## Dependências gerenciadas

As versões completas, incluindo dependências transitivas, estão nos arquivos `packages.lock.json`. As licenças abaixo foram conferidas nos metadados NuGet das versões utilizadas e, para `xunit.abstractions`, no arquivo de licença do upstream.

| Componente | Versão | Uso | Licença indicada |
| --- | --- | --- | --- |
| [Gir.Core](https://github.com/gircore/gir.core) e bindings GirCore.* | 0.8.1 | GTK4, libadwaita e bibliotecas relacionadas | MIT |
| [Microsoft.NET.Test.Sdk / TestPlatform](https://github.com/microsoft/vstest) | 17.14.1 | Testes | MIT |
| [xUnit](https://github.com/xunit/xunit), runner e analyzers | Conforme locks | Testes | Apache-2.0, incluindo [abstractions](https://github.com/xunit/abstractions.xunit/blob/main/license.txt) |
| [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) | 13.0.3 | Dependência transitiva dos testes | MIT |

O aviso MIT do Gir.Core está em [resources/licenses/GirCore-MIT.txt](resources/licenses/GirCore-MIT.txt), obtido do arquivo de licença do upstream. Os demais pacotes são restaurados pelo NuGet e não têm seu código ou binários copiados para este repositório.

## Runtime e bibliotecas do sistema

- [.NET](https://github.com/dotnet/runtime): runtime e SDK; uma publicação self-contained deve preservar os avisos de terceiros incluídos pelo SDK.
- [GTK](https://gitlab.gnome.org/GNOME/gtk), [libadwaita](https://gitlab.gnome.org/GNOME/libadwaita) e bibliotecas GNOME relacionadas: fornecidas pelo sistema e usadas dinamicamente por Gir.Core; observar os termos de cada biblioteca, incluindo LGPL, ao redistribuí-las.
- [HIDAPI](https://github.com/libusb/hidapi): biblioteca do sistema usada via P/Invoke; oferece alternativas de licenciamento descritas no [arquivo do upstream](https://github.com/libusb/hidapi/blob/master/LICENSE.txt).

A licença MIT do OpenAjazz não substitui os termos desses componentes. Uma futura distribuição binária precisa de revisão dos componentes efetivamente empacotados e de seus avisos.

## Fontes de pesquisa

Estas fontes foram consultadas para estudar transporte, protocolos e alternativas de implementação. As cópias locais de consulta e ferramentas de pesquisa não são publicadas junto ao código.

- [HIDRAW, documentação do kernel Linux](https://docs.kernel.org/hid/hidraw.html): Feature Reports e acesso em espaço de usuário.
- [HIDAPI](https://github.com/libusb/hidapi): transporte e regras de acesso.
- [AJAZZ Control Center](https://github.com/Aiacos/ajazz-control-center): notas de engenharia reversa e discussão de protocolos de outras variantes.
- [pulsar-mouse-linux](https://github.com/packerlschupfer/pulsar-mouse-linux): comparação com protocolos Nordic/Areson de famílias relacionadas.
- [python-pulsar-mouse-tool](https://github.com/andrewrabert/python-pulsar-mouse-tool): referência citada pelo projeto Pulsar consultado.
- [libratbag](https://github.com/libratbag/libratbag): organização de suporte a dispositivos e referência Sinowealth, sem comprovação de compatibilidade com o AJ199 observado.
- [Wireshark, captura USB](https://wiki.wireshark.org/CaptureSetup/USB): técnicas de observação do transporte.
- [innoextract](https://constexpr.org/innoextract/): ferramenta usada para extração local do instalador, não necessária para compilar ou executar o OpenAjazz.

O protocolo implementado está documentado em [docs/ENGENHARIA_REVERSA.md](docs/ENGENHARIA_REVERSA.md), com as diferenças entre hipóteses, observações e limites. Compatibilidade de uma família de protocolo não autoriza automaticamente escrita em outra variante.

## Fixtures e evidências

Os dois arquivos binários em `tests/Ajazz.Tests/Fixtures/` são dados pequenos capturados do dispositivo: um descritor HID e 84 bytes de configuração, sem macros ou eventos de entrada. Sua origem e interpretação estão no [README das fixtures](tests/Ajazz.Tests/Fixtures/README.md). O resumo de teste público contém apenas configurações, endereços alterados e resultados. O instalador Windows, seus recursos, executáveis, textos e desmontagens completas não são redistribuídos nem relicenciados pelo OpenAjazz.
