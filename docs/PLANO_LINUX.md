# Arquitetura e próximos passos do OpenAjazz

## Escopo atual

PoC para GNOME em C#/.NET 10: uma tela de DPI, Hz e cor do indicador do estágio ativo do AJ199 legado. Movimentação e cliques usam o suporte HID padrão do Linux. O OpenAjazz configura o mouse em espaço de usuário e não instala um driver de kernel.

O efeito de DPI foi confirmado após corrigir o índice ativo. Escrita e leitura dos registros estão implementadas; limitações físicas e compatibilidade constam em [POC.md](POC.md).

## Organização

| Projeto | Responsabilidade |
| --- | --- |
| `Ajazz.Core` | Frames, checksums, descritor HID e codificação do perfil |
| `Ajazz.Application` | Transações, comparação do estado e demonstração |
| `Ajazz.Infrastructure.Linux` | Descoberta via sysfs/HIDAPI, transporte, backup e lock |
| `Ajazz.Cli` | Consulta e configuração por linha de comando |
| `Ajazz.Gnome` | Interface GTK4/libadwaita via Gir.Core |
| `Ajazz.Tests` | Testes sem hardware e fixtures revisadas |

O transporte valida a identidade antes de configurar. A interface carrega o perfil real antes de habilitar controles. Cada aplicação salva um backup, escreve apenas os registros necessários e confirma por leitura. A demonstração é explicitamente separada da operação real.

## Próximas verificações

1. Medir a troca entre taxas e observar a cor do indicador no hardware.
2. Confirmar persistência após desligar/religar e registrar o resultado.
3. Testar cabo e variantes somente com identificação e evidências próprias.
4. Validar execução em outra instalação GNOME e documentar dependências compatíveis.
5. Avaliar empacotamento somente após essas verificações; o `.desktop` atual é um modelo.

Macros, remapeamento, daemon, novas zonas RGB e múltiplos perfis não são requisitos da publicação inicial. Qualquer expansão deve preservar a interface simples e incluir evidências suficientes para autorizar as novas escritas.

## Critério para mudanças no protocolo

Uma alteração precisa identificar modelo/firmware ou identidade observada, explicar framing e endereços, distinguir simulação de teste físico e documentar como restaurar ajustes temporários. Operações de firmware ou reset não devem ser introduzidas como tentativa de ativar configurações.

## Publicação

O [plano de publicação](PLANO_PUBLICACAO_GITHUB.md) orienta seleção de arquivos, atribuições, CI e envio ao repositório. A publicação inicial distribui código-fonte sob MIT; não exige criar um pacote ou ampliar o escopo da PoC.
