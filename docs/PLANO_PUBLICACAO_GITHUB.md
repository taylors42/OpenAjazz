# Plano de publicação do OpenAjazz no GitHub

**Nome definido:** OpenAjazz

**Repositório informado pelo autor:** https://github.com/taylors42/OpenAjazz

**Remote previsto:** `https://github.com/taylors42/OpenAjazz.git`

**Licença definida no remoto:** MIT, com aviso `Copyright (c) 2026 Taylor S`. O repositório foi consultado via Git e já contém `README.md`, `LICENSE` e a branch `main`; preservar esse histórico na integração.

## Objetivo

Publicar a PoC do configurador AJAZZ AJ199 para Linux, com interface GNOME em C#/.NET, instruções reproduzíveis e limites de compatibilidade explícitos. A publicação não depende de transformar a PoC em MVP nem de acrescentar macros, daemon, suporte genérico ou novos modelos.

**Descrição sugerida para o repositório:** Configurador experimental do mouse AJAZZ AJ199 para Linux/GNOME, desenvolvido em C# com GTK4 e libadwaita, para ajustar DPI, taxa de resposta e cor do indicador de DPI.

Este documento é um plano. Não cria repositório remoto, não escolhe uma licença em nome do autor e não publica arquivos.

## Levantamento anterior à preparação

- Código organizado em Core, Application, Infrastructure.Linux, CLI e Gnome, com solução `Ajazz.sln`.
- SDK .NET fixado em `global.json`, dependências centralizadas e arquivos de lock do NuGet.
- Interface nativa com GTK4/libadwaita via Gir.Core e transporte HIDAPI/hidraw.
- Regra udev e script de instalação já disponíveis.
- 43 testes passaram na verificação local mais recente; não substituem os testes físicos.
- O usuário confirmou a redução da sensibilidade após a correção do índice do estágio ativo e a reaplicação de 500 DPI.
- Observação dos relatórios de movimento compatível com os 1000 Hz atuais; mudança entre taxas e efeito visual da cor ainda precisam de confirmação física.
- A pasta local ainda não é um repositório Git nem contém licença ou workflows de CI. O remoto já contém README e licença MIT; trazer a licença existente ao integrar o histórico.
- Há instalador Windows, arquivos extraídos, ferramentas de análise e diagnósticos locais junto ao código.

## Preparação da publicação inicial

A seleção de arquivos foi preparada conforme este plano: instaladores, materiais extraídos, ferramentas e diagnósticos brutos permanecem ignorados em `analysis/`; somente fixtures revisadas, uma captura da própria interface e um resumo de evidência foram selecionados. README, protocolo e descrição das fixtures foram corrigidos. A CI sem hardware, atribuições e templates de contribuição estão incluídos.

O remoto foi consultado: branch `main`, licença MIT e visibilidade **privada**. A autorização de envio é para esse repositório; tornar o projeto público é uma decisão separada do proprietário. O histórico inicial e o aviso de autoria são preservados na integração.

A publicação inicial contém código-fonte. Não inclui uma release binária nem afirma testes físicos ainda pendentes como aprovados. Os resultados de CI ficam disponíveis na aba Actions do repositório.

## 1. Preparar o conteúdo público

### Incluir no repositório

- `src/`, `tests/`, `resources/` e os scripts necessários para desenvolver e executar a aplicação.
- `Ajazz.sln`, `global.json`, `Directory.Build.props` e `Directory.Packages.props`.
- Arquivos `packages.lock.json` dos projetos.
- README e documentação revisada, incluindo a especificação parcial do protocolo.
- Fixtures pequenas, com origem documentada e conteúdo revisado.
- Uma captura atual da própria aplicação, sem caminhos pessoais ou outros elementos do desktop.

### Manter fora do primeiro commit

- `AJAZZ AJ199 Setup v1.0 20231205.exe` e quaisquer outros instaladores ou binários proprietários.
- `analysis/extracted/`, recursos e textos extraídos integralmente do software original.
- `analysis/tools/`, ferramentas baixadas para pesquisa.
- `analysis/references/`, cópias de código de terceiros usadas como referência. Manter atribuições e links na documentação.
- Desmontagens completas, dumps de strings e demais saídas brutas de engenharia reversa.
- Diagnósticos, inventários e capturas locais não revisados; backups de configurações pessoais.
- `bin/`, `obj/`, `artifacts/`, resultados de testes, arquivos de IDE e credenciais.

**Execução:** ampliar o `.gitignore` antes de usar `git add`. Como ponto de partida, ignorar `analysis/` inteiro e mover somente evidências selecionadas e revisadas para uma pasta pública, como `docs/evidencias/`. Não apagar os materiais locais de pesquisa.

**Aceite:** a lista de arquivos preparados para o primeiro commit contém apenas código, documentos e recursos intencionalmente públicos. Os links da documentação não dependem de arquivos ignorados.

## 2. Definir identidade, licença e atribuições

- Nome, slug e proprietário definidos pelo autor: `taylors42/OpenAjazz`. Usar **OpenAjazz** na apresentação pública; manter a compatibilidade restrita ao AJ199 legado enquanto outras variantes não forem verificadas.
- Apresentar o projeto como experimental e independente, sem sugerir vínculo oficial com a AJAZZ.
- Usar a licença MIT já escolhida pelo autor no remoto. Trazer o arquivo `LICENSE` existente, preservando o titular, em vez de criar uma licença divergente.
- Revisar as licenças das dependências e identificar avisos exigidos para redistribuição dos artefatos publicados.
- Registrar fontes e atribuições de pesquisa em `docs/REFERENCIAS.md` ou documento equivalente, distinguindo consulta de material e incorporação de código.
- Revisar a procedência das fixtures; a licença do projeto não deve ser apresentada como autorização de redistribuição do instalador ou de arquivos de terceiros.

**Aceite:** licença escolhida pelo autor, atribuições presentes e arquivos de terceiros tratados conforme sua procedência.

## 3. Revisar README e documentação

O README deve permitir que uma pessoa entenda e experimente a PoC sem ler todo o histórico de pesquisa:

1. Objetivo e captura da interface atual.
2. Estado experimental e compatibilidade observada: AJ199 legado, identidade `40:01`, receptor `25A7:FA7C`.
3. Funcionalidades: leitura do perfil, ajuste do DPI do estágio ativo, Hz e cor do indicador de DPI.
4. Dependências nativas, SDK e comandos de execução verificados em uma instalação limpa.
5. Instalação da regra udev; a interface executa como usuário normal.
6. Como acordar o mouse, atualizar o perfil e reconhecer falhas de acesso ou comunicação.
7. Backups locais e diferença entre Desfazer na interface e restaurar configurações do aparelho.
8. Limites e estado dos testes físicos.
9. Como contribuir, reportar problemas e consultar o protocolo.

### Correções específicas já identificadas

- Remover do README a apresentação do teste antigo de memória como comprovação geral do funcionamento físico.
- Documentar o índice de estágio ativo a partir de zero e a confirmação de DPI obtida após sua correção.
- Corrigir `tests/Ajazz.Tests/Fixtures/README.md`: na fixture, o índice ativo 1 corresponde ao segundo registro, cuja cor é verde; a descrição atual menciona vermelho.
- Não afirmar que a cor foi observada fisicamente ou que a troca de Hz foi medida antes de realizar esses testes.
- Não apresentar o dispositivo por cabo `25A7:FA7B` como validado apenas porque está previsto na descoberta.
- Substituir links para arquivos locais de pesquisa por evidências públicas revisadas ou por descrições suficientes no documento.
- Atualizar o comentário do arquivo `.desktop`, que ainda descreve configurações simuladas. Publicar instruções de instalação somente quando `Exec` corresponder a um executável efetivamente instalado.
- Separar claramente análise histórica, hipóteses descartadas e comportamento confirmado.

**Aceite:** as instruções funcionam a partir de um clone novo e nenhuma alegação de compatibilidade excede as evidências.

## 4. Verificar a PoC antes da publicação

### Verificação sem hardware

Executar em um diretório limpo, usando apenas arquivos destinados ao repositório:

```sh
dotnet restore Ajazz.sln --locked-mode
dotnet build Ajazz.sln --no-restore
dotnet test Ajazz.sln --no-restore
```

- Confirmar que as fixtures necessárias estão incluídas.
- Garantir que scripts e projetos não dependem de caminhos pessoais ou de `analysis/extracted/`.
- Revisar os arquivos preparados para commit em busca de tokens, dados pessoais e conteúdo acidental.
- Verificar a demonstração gráfica separadamente, em ambiente com GTK/libadwaita e sessão gráfica adequados.

### Verificação física documentada

- Ler o estado atual e preservar o backup antes de alterar configurações.
- Confirmar DPI no estágio ativo, incluindo a regressão do índice zero.
- Alterar a taxa e observar os intervalos de relatórios durante movimento contínuo; distinguir frequência do sensor/receptor de taxa de quadros da interface.
- Confirmar quando e onde o indicador mostra a cor configurada.
- Testar mouse em repouso, receptor removido e perfil alterado fora da aplicação.
- Verificar persistência após desligar e religar o mouse antes de fazer essa promessa no README.
- Restaurar os ajustes anteriores ao terminar testes temporários.

Uma verificação física pendente pode permanecer como limitação pública da PoC. Não deve ser descrita como aprovada.

**Aceite:** build e testes reproduzíveis; resultados físicos registrados com método, ambiente e limitações.

## 5. Adicionar integração contínua

Criar `.github/workflows/ci.yml` para pushes e pull requests:

- Instalar o SDK compatível com `global.json`.
- Restaurar com `--locked-mode`, compilar e executar os testes sem hardware.
- Usar permissões mínimas, suficientes para ler o código.
- Escolher ações e versões suportadas consultando a documentação oficial no momento da implementação; fixar referências de forma reproduzível.
- Não abrir dispositivos HID nem instalar regras udev na CI comum.
- Não incluir o smoke test gráfico sem preparar explicitamente bibliotecas nativas e um ambiente gráfico compatível.

**Aceite:** um clone limpo passa pela CI sem depender desta máquina, de um receptor conectado ou de segredos.

## 6. Preparar colaboração

- Criar `CONTRIBUTING.md` com build, testes, estrutura e critérios para mudanças de protocolo.
- Criar template de bug solicitando versão do projeto, distribuição, sessão gráfica, modelo, VID/PID, mensagem exibida e passos para reproduzir.
- Orientar a remoção de dados pessoais dos diagnósticos e evitar solicitar dumps de entrada, macros ou perfis completos sem necessidade.
- Criar template de pull request com descrição, verificações e indicação de eventual teste físico.
- Registrar limites de suporte a variantes e exigir evidência antes de adicionar uma identidade de dispositivo ao caminho de escrita.

**Aceite:** contribuidores conseguem reproduzir problemas e propor mudanças sem acesso aos materiais privados de pesquisa.

## 7. Criar o repositório e publicar o código

Depois dos aceites anteriores:

1. Inicializar Git com a branch `main`.
2. Adicionar explicitamente os arquivos públicos selecionados.
3. Conferir `git status`, `git diff --cached --stat` e `git diff --cached` antes do primeiro commit.
4. Criar o commit inicial.
5. Usar o repositório já criado pelo autor: `https://github.com/taylors42/OpenAjazz`. Conferir sua visibilidade e os arquivos existentes antes de conectar o código local.
6. Configurar `origin` como `https://github.com/taylors42/OpenAjazz.git`. Se houver commits no remoto, integrar o histórico sem sobrescrevê-lo. Publicar `main` quando a preparação estiver concluída e o envio tiver sido solicitado pelo autor.
7. Preencher descrição e tópicos, como `linux`, `gnome`, `csharp`, `gtk4`, `libadwaita`, `hid` e `ajazz`.
8. Conferir a renderização dos documentos e o resultado da CI no GitHub.

Não usar uma adição indiscriminada de toda a pasta antes de revisar o conteúdo. Não inicializar o remoto com arquivos conflitantes com o commit local já preparado.

**Aceite:** repositório acessível, CI aprovada e documentação com links válidos.

## 8. Publicar uma versão experimental, se desejado

A publicação inicial pode disponibilizar somente o código-fonte. Um binário é uma etapa separada:

- Escolher uma versão que identifique a PoC como experimental.
- Gerar o artefato a partir de um commit/tag conhecido.
- Publicar para Linux x86-64 apenas enquanto essa for a arquitetura verificada.
- Informar que self-contained inclui o runtime .NET, mas ainda depende de GTK4, libadwaita e HIDAPI do sistema.
- Fornecer instruções de execução, regra udev, checksums SHA-256 e notas da versão.
- Não incluir instaladores Windows, ferramentas de análise, backups ou logs locais no pacote.
- Não anunciar RPM, Flatpak ou AppImage sem implementar e testar o respectivo formato.

**Aceite:** o artefato corresponde à tag e executa em um ambiente compatível seguindo as instruções publicadas.

## Ordem sugerida

| Etapa | Entrega | Prioridade |
| --- | --- | --- |
| 1 | Lista pública de arquivos e `.gitignore` revisado | Antes do primeiro commit |
| 2 | Nome, licença e atribuições | Antes da publicação |
| 3 | README e documentação corrigidos | Antes da publicação |
| 4 | Build, testes e revisão do conteúdo | Antes da publicação |
| 5 | CI sem hardware | Na publicação inicial |
| 6 | Instruções de contribuição e templates | Na publicação inicial |
| 7 | Repositório remoto e código publicado | Após os aceites anteriores |
| 8 | Artefato experimental e notas da versão | Opcional, após o código |

## Checklist final

- [x] Nome e proprietário definidos: `taylors42/OpenAjazz`.
- [x] Licença MIT definida no remoto.
- [x] Visibilidade conferida.
- [x] Instalador, arquivos extraídos, ferramentas e diagnósticos privados fora do commit.
- [x] Fixtures revisadas e origem documentada.
- [x] Índice do estágio ativo e evidências atualizados nos documentos.
- [x] Limites de DPI, Hz, cor, cabo e persistência descritos com precisão.
- [x] Restore, build e testes verificados em cópia limpa dos arquivos selecionados; instalação em outra máquina permanece futura.
- [x] Restore, build e testes aprovados.
- [x] CI configurada sem depender de hardware.
- [x] Arquivos preparados para commit revisados.
- [ ] README e links conferidos no GitHub após publicação.
