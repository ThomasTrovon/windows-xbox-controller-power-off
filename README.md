<div align="center">

# Xbox Controller Off

Desligue instantaneamente os controles Xbox conectados ao adaptador oficial do Windows — sem remover pilhas, cabos ou pareamentos.

![Windows 10 e 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows)
![.NET 10 LTS](https://img.shields.io/badge/.NET-10%20LTS-512BD4?logo=dotnet)
![Plataforma x64](https://img.shields.io/badge/plataforma-x64-informational)
![Controles](https://img.shields.io/badge/controles-at%C3%A9%208-success)

</div>

## O que é

O **Xbox Controller Off** é um utilitário pequeno para Windows que desliga controles Xbox One, Xbox Series X|S e Xbox Elite conectados pelo **Xbox Wireless Adapter oficial**.

O Windows não oferece um botão visível para desligar esses controles. Normalmente é necessário segurar o botão Xbox por vários segundos ou remover as pilhas. Este aplicativo envia ao driver do próprio Windows o mesmo comando interno de desligamento utilizado pela pilha Xbox GIP.

Esta versão é uma modernização não oficial do projeto [mendhak/xbox-controller-off](https://github.com/mendhak/xbox-controller-off), originalmente criado para a geração Xbox 360 com XInput 1.3.

## Destaques

- desliga todos os controles encontrados com uma única execução;
- reconhece até **oito controles simultâneos**, o limite do adaptador moderno;
- funciona com Xbox One, Xbox Series X|S e Xbox Elite pelo protocolo GIP;
- preserva o pareamento: basta apertar o botão Xbox para reconectar;
- não instala drivers, serviços ou tarefas permanentes;
- gera um executável único e autossuficiente para Windows x64;
- registra o resultado detalhado da última execução;
- não possui telemetria e não realiza conexões de rede.

## Compatibilidade

| Conexão ou dispositivo | Situação |
|---|---|
| Xbox Wireless Adapter oficial + controle Xbox Series | ✅ Testado em hardware real |
| Xbox Wireless Adapter oficial + controle Xbox One/Elite GIP | ✅ Compatível pelo mesmo protocolo |
| Até oito controles no mesmo adaptador | ✅ Implementado |
| Controle conectado por cabo USB e reconhecido pelo `xboxgip.sys` | 🟡 Compatível pelo protocolo; ainda não validado neste projeto |
| Bluetooth | ❌ Não compatível com o comando administrativo GIP |
| Controle/receptor Xbox 360 | ❌ Utilize a versão XInput do projeto original |
| Windows de 32 bits | ❌ Não compatível |

## Requisitos

- Windows 10 ou Windows 11 de 64 bits;
- Xbox Wireless Adapter oficial;
- controle compatível conectado pelo protocolo Xbox GIP;
- autorização de administrador no aviso do UAC.

O executável publicado é autossuficiente. O usuário final não precisa instalar o .NET separadamente.

## Como usar

1. Execute `XBoxControllerOff.exe`.
2. Aceite o aviso do Controle de Conta de Usuário.
3. Aguarde um instante: todos os controles encontrados serão desligados.

O aplicativo não remove nem altera o pareamento. Para usar o controle novamente, aperte normalmente o botão Xbox.

### Log da última execução

O resultado completo fica salvo em:

```text
C:\ProgramData\XboxControllerOff\ultima-execucao.log
```

Exemplo de uma execução bem-sucedida:

```text
Trabalhador executando como: NT AUTHORITY\SYSTEM
Horário: 2026-08-11 21:49:43
Encontrado(s) 1 controle(s):
  - 0000D7F32583ED7E
Desligar 0000D7F32583ED7E: OK
Todos os controles encontrados foram desligados.
RESULT=0
```

### Códigos de resultado

| Código | Significado |
|---:|---|
| `0` | Todos os controles encontrados foram desligados |
| `1` | Falha de execução, permissão ou comunicação com o driver |
| `2` | Nenhum controle GIP conectado foi encontrado |
| `3` | Um ou mais controles foram encontrados, mas não puderam ser desligados |

## Como funciona

Controles Xbox One e Series conectados pelo adaptador moderno utilizam o **Game Input Protocol (GIP)**, não o caminho legado de desligamento do XInput.

O driver `xboxgip.sys` do Windows expõe internamente duas interfaces:

| Interface | Finalidade |
|---|---|
| `\\.\XboxGIP` | Enumeração e comunicação normal com dispositivos GIP |
| `\\.\XboxGIP_Admin` | Operações administrativas do driver |

Durante a execução, o aplicativo:

1. cria uma tarefa agendada temporária com um nome aleatório;
2. executa um trabalhador como `NT AUTHORITY\SYSTEM`;
3. solicita ao driver a reenumeração dos controles GIP;
4. coleta até oito identificadores de dispositivo;
5. envia o IOCTL `0x40001C4C` com o subcomando `0x02` (`ControlTurnOff`) para cada controle;
6. grava o resultado no log;
7. remove a tarefa temporária em um bloco de limpeza, inclusive quando ocorre uma falha.

O acesso como `SYSTEM` é necessário porque o driver não permite que um processo administrativo comum abra `\\.\XboxGIP_Admin` para essa operação.

## Segurança e limitações

- Nenhum driver é instalado, substituído ou modificado.
- Nenhum serviço ou tarefa agendada permanece depois da execução.
- O UAC é esperado porque a criação da tarefa SYSTEM temporária exige privilégios administrativos.
- O aplicativo envia somente o subcomando `ControlTurnOff`; comandos de redefinição, firmware e associação não são utilizados.
- A interface administrativa e o IOCTL são internos e não possuem garantia pública de estabilidade. Uma atualização futura do Windows pode exigir uma adaptação.

## Solução de problemas

### O controle não foi encontrado

Confira se:

- o controle está ligado;
- ele está conectado pelo Xbox Wireless Adapter, e não por Bluetooth;
- o adaptador aparece no Gerenciador de Dispositivos como `Xbox Wireless Adapter for Windows`;
- o arquivo de log informa `RESULT=2` ou apresenta algum erro específico.

### O controle aparece no jogo, mas não desliga

Se a conexão for Bluetooth, o controle pode aparecer normalmente como XInput, mas não estará acessível pela interface administrativa do adaptador. Pareie-o diretamente com o Xbox Wireless Adapter.

### Verificar tarefas residuais

O aplicativo remove a tarefa no final. Para confirmar manualmente:

```powershell
Get-ScheduledTask -TaskName 'XboxControllerOff-*' -ErrorAction SilentlyContinue
```

Uma execução normal não retorna nenhuma tarefa.

## Desenvolvimento e compilação

O projeto utiliza o .NET SDK 10.0.302, fixado em `global.json`.

### Restaurar e compilar

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' restore '.\XBoxControllerOff\XBoxControllerOff.csproj'
& 'C:\Program Files\dotnet\dotnet.exe' build '.\XBoxControllerOff\XBoxControllerOff.sln' --configuration Release
```

### Publicar o executável autossuficiente

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' publish '.\XBoxControllerOff\XBoxControllerOff.csproj' --configuration Release
```

O resultado será criado em:

```text
XBoxControllerOff\bin\Release\net10.0-windows\win-x64\publish\XBoxControllerOff.exe
```

O arquivo `TurnControllerOff.ps1` foi preservado para manter um ponto de entrada compatível com o projeto original. Ele apenas localiza e inicia o executável moderno; a implementação GIP permanece centralizada no aplicativo C#.

## Estrutura do projeto

```text
.
├── XBoxControllerOff/
│   ├── Program.cs                  # Aplicativo, enumeração GIP e desligamento
│   ├── XBoxControllerOff.csproj    # Projeto .NET 10 para Windows x64
│   ├── XBoxControllerOff.sln       # Solução do Visual Studio
│   └── app.manifest                # Solicitação de elevação pelo UAC
├── TurnControllerOff.ps1           # Lançador compatível com o projeto original
├── global.json                     # Versão do SDK utilizada na compilação
└── THIRD_PARTY_NOTICES.md          # Atribuições e licenças de referência
```

## Créditos

- [Mendhak](https://github.com/mendhak), autor do projeto original `xbox-controller-off`;
- [Vektast/XBOX_Controller_PW_OFF](https://github.com/Vektast/XBOX_Controller_PW_OFF), implementação GIP em PowerShell usada como referência técnica;
- [Leclowndu93150/xbpoweroff](https://github.com/Leclowndu93150/xbpoweroff), implementação GIP em C usada como referência técnica;
- documentação pública [Microsoft GameInput Protocol USB](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-gipusb/e7c90904-5e21-426e-b9ad-d82adeee0dbc).

Os avisos integrais das implementações de referência estão preservados em [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
