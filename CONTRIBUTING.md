# Contribuir com o OpenAjazz

O escopo atual é uma PoC para configurar DPI, Hz e cor do indicador de DPI do AJ199 legado em uma tela GNOME. Consulte [o README](README.md) e [a arquitetura](docs/PLANO_LINUX.md) antes de propor expansão para outros modelos.

## Preparação e verificações

Instale o SDK indicado em `global.json`. Para compilar e testar sem mouse:

```sh
dotnet restore Ajazz.sln --locked-mode
dotnet build Ajazz.sln -c Release --no-restore
dotnet test Ajazz.sln -c Release --no-build --no-restore
```

A GUI requer as bibliotecas nativas e uma sessão gráfica, conforme o README. `--smoke-test` usa demonstração; `--hardware-smoke-test` somente lê o aparelho e não é executado na CI. Feche outra instância antes dos testes gráficos.

Mantenha `packages.lock.json` versionado. Ao alterar dependências, atualize os locks intencionalmente e revise licenças e atribuições. A solução trata warnings como erros.

## Alterações de protocolo

- Identifique o modelo e a identidade observada; VID/PID sozinho não comprova compatibilidade.
- Explique endereços, tamanhos, checksums e quais registros serão escritos.
- Inclua testes de regressão significativos e indique quais verificações foram simuladas ou físicas.
- Preserve os demais campos e o backup antes da primeira escrita.
- Não envie comandos desconhecidos de reset ou firmware como tentativa de aplicar configurações.
- Registre limitações e restaure os valores anteriores ao terminar testes temporários.

A validação dos índices de estágio usa a convenção a partir de zero documentada no protocolo. Não deduza o índice a partir da quantidade de estágios.

## Relatar problemas

Use o template de bug com distribuição, versão do projeto, sessão gráfica, modelo e mensagem exibida. Não envie credenciais, caminhos pessoais, dumps de eventos de entrada, backups completos ou arquivos proprietários. Se compartilhar um inventário do dispositivo, revise seu conteúdo primeiro.

## Pull requests

Descreva o problema, o comportamento resultante e as verificações realizadas. Não inclua saídas de build, instaladores, código de terceiros sem atribuição ou materiais locais de pesquisa. Contribuições aceitas integram o código sob a licença [MIT](LICENSE), preservadas as atribuições aplicáveis.
