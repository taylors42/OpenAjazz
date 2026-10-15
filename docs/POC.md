# PoC do OpenAjazz

A execução normal consulta e configura o AJ199 legado. A demonstração só é ativada explicitamente por `--smoke-test`. Esta entrega mantém uma tela para DPI, Hz e cor do indicador de DPI.

## Evidências de hardware

Verificação realizada no receptor `25A7:FA7C`, identidade `CID 0x40 / MID 0x01`, em Fedora 44/GNOME 50.5/Wayland.

| Verificação | Resultado |
| --- | --- |
| Identidade, estado de conexão e perfil | Respostas reais recebidas e validadas |
| Gravação e leitura dos registros | Confirmadas |
| DPI do estágio ativo | 1600 → 500; usuário confirmou redução da sensibilidade |
| Preservação dos demais registros nessa alteração | Confirmada pela comparação dos 84 bytes |
| Taxa atual de 1000 Hz | 763 relatórios observados; mediana de 0,999804 ms entre os intervalos contínuos |
| Troca entre taxas de resposta | Ainda não medida fisicamente |
| Cor do indicador | Registro gravado e relido; efeito visual pendente |
| Persistência após desligar/religar | Pendente |
| Conexão por cabo e outras variantes | Pendentes |

O resumo revisado está em [aj199-stage-fix.json](evidencias/aj199-stage-fix.json). Não inclui caminhos pessoais, inventários do computador nem eventos de entrada. Somente uma captura da própria janela em demonstração foi selecionada para publicação.

### Correção do índice do estágio

Uma versão anterior subtraía 1 do índice ativo e escrevia no estágio anterior. O teste antigo confirmou apenas a memória desse estágio, sem comprovar o efeito no sensor.

O driver original carrega o índice bruto de `0x04` e o usa diretamente em arrays. A aplicação agora usa esse índice a partir de zero. No receptor observado, quantidade=1 e índice ativo=1 coexistem; ambos são preservados, e o índice é validado contra os oito registros de hardware. Testes de regressão verificam os endereços dos índices 0, 1 e 7.

A reaplicação corrigida gravou 500 DPI / 1000 Hz / `#F6D32D` no índice ativo 1. Apenas bytes de `0x10..13` e `0x30..33` mudaram; o estágio anterior foi preservado. O usuário confirmou o efeito na sensibilidade.

## Transação de configuração

1. Validar a identidade e verificar se o mouse está conectado.
2. Abrir a sessão de configuração e ler o perfil de hardware 0.
3. Validar checksums e comparar a memória com a última leitura da interface.
4. Salvar um backup antes da primeira escrita; falha no backup impede a escrita.
5. Gravar apenas os registros alterados.
6. Reler e comparar os bytes escritos; divergência não é tratada como sucesso.
7. Encerrar a sessão, inclusive em caso de falha.

Um lock entre processos serializa as operações da GUI e da CLI desta aplicação. Ele não impede alterações por ferramentas externas. Após iniciar uma escrita, o fechamento da janela espera a conclusão da transação limitada e sua verificação.

Não há reset de fábrica, atualização de firmware nem envio do opcode `0x09`. Desfazer na interface não restaura um backup no aparelho; não existe comando de restauração de backup nesta PoC.

## Execução e acesso

Siga os requisitos, build e instalação da regra udev no [README](../README.md). Se o receptor estiver presente mas o mouse estiver em repouso ou desconectado, mova ou ligue o mouse e atualize. Quando a leitura inicial falha, a janela tenta novamente a cada três segundos sem preencher os controles com valores fictícios.

O script `scripts/inspect_linux.py` gera um inventário por sysfs sem abrir hidraw. Sua saída pode conter propriedades do dispositivo e deve ser revisada antes de compartilhar. O inventário não faz parte do repositório.

## Publicação local

```sh
dotnet restore Ajazz.sln --locked-mode
dotnet publish src/Ajazz.Gnome -c Release -r linux-x64 --self-contained true -o artifacts/poc/linux-x64 --no-restore
./artifacts/poc/linux-x64/Ajazz.Gnome
```

A publicação inclui o runtime .NET; ainda depende das bibliotecas nativas do sistema. Não há pacote RPM, Flatpak ou AppImage nesta entrega. O arquivo `.desktop` em `resources/desktop/` é um modelo; seu `Exec=Ajazz.Gnome` exige que o executável seja instalado no PATH antes de instalar o lançador.

## Verificação

```sh
dotnet test Ajazz.sln --no-restore
./artifacts/poc/linux-x64/Ajazz.Gnome --smoke-test
./artifacts/poc/linux-x64/Ajazz.Gnome --hardware-smoke-test
```

Os 43 testes unitários cobrem framing, checksums, perfil capturado, conversão DPI, endereços do estágio ativo, preservação de campos, falhas de backup, estado desatualizado e confirmação divergente. Não acessam o mouse.

O smoke test gráfico de demonstração encerra com `GNOME_SMOKE_OK`. O teste de hardware somente lê e retorna `HARDWARE_PROFILE_READ_OK` ou código 3 quando o perfil está indisponível; requer mouse acordado, bibliotecas nativas e sessão gráfica. Feche outra instância da aplicação antes dos smoke tests, pois a aplicação é de instância única.

`AJAZZ_POC_SCREENSHOT=/caminho/tela.png` salva somente a própria janela durante esses testes. Não execute os testes gráficos na CI sem preparar as dependências e a sessão gráfica.
