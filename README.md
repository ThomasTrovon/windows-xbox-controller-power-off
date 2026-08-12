<div align="center">

## English

# Xbox Controller Off

Instantly turn off Xbox controllers connected to the official Windows adapter — without removing batteries, unplugging cables, or clearing pairings.

![Windows 10 and 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows)
![.NET 10 LTS](https://img.shields.io/badge/.NET-10%20LTS-512BD4?logo=dotnet)
![x64 platform](https://img.shields.io/badge/platform-x64-informational)
![Controllers](https://img.shields.io/badge/controllers-up%20to%208-success)

[![Download the latest release](https://img.shields.io/badge/Download-Latest%20Release-2ea44f?style=for-the-badge&logo=github)](https://github.com/ThomasTrovon/xbox-controller-off/releases/latest)

**[Open the download page and get the latest `XBoxControllerOff.exe`](https://github.com/ThomasTrovon/xbox-controller-off/releases/latest)**

</div>

## What it is

**Xbox Controller Off** is a small Windows utility that turns off Xbox One, Xbox Series X|S, and Xbox Elite controllers connected through the **official Xbox Wireless Adapter**.

Windows does not provide a visible button for turning these controllers off. Users normally have to hold the Xbox button for several seconds or remove the batteries. This application asks the Windows driver to send the same internal shutdown command used by the Xbox GIP stack.

This version is an unofficial modernization of [mendhak/xbox-controller-off](https://github.com/mendhak/xbox-controller-off), which was originally created for the Xbox 360 generation using XInput 1.3.

## Highlights

- turns off every detected controller in a single run;
- detects up to **eight simultaneous controllers**, the limit of the modern adapter;
- supports Xbox One, Xbox Series X|S, and Xbox Elite controllers through GIP;
- preserves pairing: press the Xbox button to reconnect;
- installs no drivers, services, or permanent scheduled tasks;
- produces a single, self-contained executable for 64-bit Windows;
- records a detailed result for the latest run;
- contains no telemetry and makes no network connections.

## Compatibility

| Connection or device | Status |
|---|---|
| Official Xbox Wireless Adapter + Xbox Series controller | ✅ Tested on real hardware |
| Official Xbox Wireless Adapter + Xbox One/Elite GIP controller | ✅ Compatible through the same protocol |
| Up to eight controllers on one adapter | ✅ Implemented |
| Controller connected by USB and handled by `xboxgip.sys` | 🟡 Protocol-compatible; not yet validated by this project |
| Bluetooth | ❌ Not compatible with the administrative GIP command |
| Xbox 360 controller/receiver | ❌ Use the XInput version from the original project |
| 32-bit Windows | ❌ Not supported |

## Requirements

- 64-bit Windows 10 or Windows 11;
- an official Xbox Wireless Adapter;
- a compatible controller connected through Xbox GIP;
- administrator approval in the UAC prompt.

The published executable is self-contained. End users do not need to install .NET separately.

## How to use

1. Run `XBoxControllerOff.exe`.
2. Accept the User Account Control prompt.
3. Wait a moment: every detected controller will be turned off.

The application does not remove or change the pairing. To use a controller again, press its Xbox button normally.

### Latest-run log

The complete result is saved to:

```text
C:\ProgramData\XboxControllerOff\ultima-execucao.log
```

Example of a successful run:

```text
Trabalhador executando como: NT AUTHORITY\SYSTEM
Horário: 2026-08-11 21:49:43
Encontrado(s) 1 controle(s):
  - 0000D7F32583ED7E
Desligar 0000D7F32583ED7E: OK
Todos os controles encontrados foram desligados.
RESULT=0
```

The application currently writes operational log messages in Brazilian Portuguese. The numeric result codes below remain language-independent.

### Result codes

| Code | Meaning |
|---:|---|
| `0` | Every detected controller was turned off |
| `1` | Execution, permission, or driver communication failure |
| `2` | No connected GIP controller was found |
| `3` | One or more controllers were found but could not be turned off |

## How it works

Xbox One and Series controllers connected through the modern adapter use the **Game Input Protocol (GIP)** rather than the legacy XInput shutdown path.

The Windows `xboxgip.sys` driver internally exposes two interfaces:

| Interface | Purpose |
|---|---|
| `\\.\XboxGIP` | GIP device enumeration and regular communication |
| `\\.\XboxGIP_Admin` | Administrative driver operations |

During a run, the application:

1. creates a temporary scheduled task with a random name;
2. runs a worker as `NT AUTHORITY\SYSTEM`;
3. asks the driver to re-enumerate GIP controllers;
4. collects up to eight device identifiers;
5. sends IOCTL `0x40001C4C` with subcommand `0x02` (`ControlTurnOff`) to each controller;
6. writes the result to the log;
7. deletes the temporary task from a cleanup block, including when a failure occurs.

Running as `SYSTEM` is necessary because the driver does not allow a regular administrative process to open `\\.\XboxGIP_Admin` for this operation.

## Security and limitations

- No driver is installed, replaced, or modified.
- No service or scheduled task remains after execution.
- The UAC prompt is expected because creating the temporary SYSTEM task requires administrative privileges.
- The application sends only the `ControlTurnOff` subcommand; reset, firmware, and association commands are not used.
- The administrative interface and IOCTL are internal and have no public stability guarantee. A future Windows update may require an adaptation.

## Troubleshooting

### The controller was not found

Check whether:

- the controller is turned on;
- it is connected through the Xbox Wireless Adapter rather than Bluetooth;
- Device Manager lists the adapter as `Xbox Wireless Adapter for Windows`;
- the log reports `RESULT=2` or provides a more specific error.

### The controller works in games but does not turn off

A Bluetooth controller can appear normally through XInput without being accessible through the adapter's administrative interface. Pair it directly with the Xbox Wireless Adapter.

### Check for leftover tasks

The application removes its task when it finishes. To verify this manually:

```powershell
Get-ScheduledTask -TaskName 'XboxControllerOff-*' -ErrorAction SilentlyContinue
```

A normal run returns no tasks.

## Development and build

The project uses .NET SDK 10.0.302, pinned in `global.json`.

### Restore and build

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' restore '.\XBoxControllerOff\XBoxControllerOff.csproj'
& 'C:\Program Files\dotnet\dotnet.exe' build '.\XBoxControllerOff\XBoxControllerOff.sln' --configuration Release
```

### Publish the self-contained executable

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' publish '.\XBoxControllerOff\XBoxControllerOff.csproj' --configuration Release
```

The result will be created at:

```text
XBoxControllerOff\bin\Release\net10.0-windows\win-x64\publish\XBoxControllerOff.exe
```

`TurnControllerOff.ps1` is kept as an entry point compatible with the original project. It only locates and starts the modern executable; the GIP implementation remains centralized in the C# application.

## Project structure

```text
.
├── XBoxControllerOff/
│   ├── Program.cs                  # Application, GIP enumeration, and shutdown
│   ├── XBoxControllerOff.csproj    # .NET 10 project for 64-bit Windows
│   ├── XBoxControllerOff.sln       # Visual Studio solution
│   └── app.manifest                # UAC elevation request
├── TurnControllerOff.ps1           # Entry point compatible with the original project
├── global.json                     # SDK version used by the build
└── THIRD_PARTY_NOTICES.md          # Attributions and reference licenses
```

## Credits

- [Mendhak](https://github.com/mendhak), author of the original `xbox-controller-off` project;
- [Vektast/XBOX_Controller_PW_OFF](https://github.com/Vektast/XBOX_Controller_PW_OFF), GIP PowerShell implementation used as a technical reference;
- [Leclowndu93150/xbpoweroff](https://github.com/Leclowndu93150/xbpoweroff), GIP C implementation used as a technical reference;
- public [Microsoft GameInput Protocol USB](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-gipusb/e7c90904-5e21-426e-b9ad-d82adeee0dbc) documentation.

The complete notices for the reference implementations are preserved in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

---

<div align="center">

## Português (Brasil)

</div>

<div align="center">

# Xbox Controller Off

Desligue instantaneamente os controles Xbox conectados ao adaptador oficial do Windows — sem remover pilhas, cabos ou pareamentos.

![Windows 10 e 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows)
![.NET 10 LTS](https://img.shields.io/badge/.NET-10%20LTS-512BD4?logo=dotnet)
![Plataforma x64](https://img.shields.io/badge/plataforma-x64-informational)
![Controles](https://img.shields.io/badge/controles-at%C3%A9%208-success)

[![Baixar a versão mais recente](https://img.shields.io/badge/Baixar-Vers%C3%A3o%20mais%20recente-2ea44f?style=for-the-badge&logo=github)](https://github.com/ThomasTrovon/xbox-controller-off/releases/latest)

**[Abra a página de download e baixe o `XBoxControllerOff.exe` mais recente](https://github.com/ThomasTrovon/xbox-controller-off/releases/latest)**

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
