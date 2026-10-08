# Pro Orçamento

Aplicativo Android de orçamento financeiro familiar, construído com **.NET MAUI Blazor Hybrid**. Funciona 100% offline (local-first): todos os dados ficam em um banco SQLite no próprio dispositivo, sem conta, login ou servidor.

## Funcionalidades

- **Dashboard** com saldo do mês, receitas, despesas pagas e a pagar, alertas de orçamento, gráficos por categoria e tendência mensal.
- **Lançamentos** com cabeçalho fixo (mês, filtros, KPIs e gráfico de linhas com a evolução acumulada de receitas, despesas, despesas pagas e saldo), marcação de pago/não pago e menu de ações flutuante (FAB).
- **Fechamento de mês**: trava o período para impedir inclusões, alterações e exclusões; pode ser reaberto a qualquer momento. Cada fechamento/reabertura fica registrado em **Logs**.
- **Trazer mês anterior**: copia despesas recorrentes e limites de orçamento do mês anterior.
- **Orçamento mensal** por categoria, com comparativo com o mês anterior e meta de redução percentual.
- **Metas de economia** com contribuições (opcionalmente lançadas como despesa).
- **Relatórios** exportáveis em CSV.
- **Meu Perfil**: nome, apelido e foto (selfie pela câmera ou imagem da galeria).
- **Configurações**:
  - Notificações de despesas: ativar/desativar, som sim/não e prazos (3 dias antes, 1 dia antes, no dia, 1 dia depois e diariamente enquanto vencida).
  - Armazenamento: espaço ocupado (banco, logs, foto), contagem de registros e limpeza seletiva (lançamentos, orçamento mensal, metas ou tudo).
- **Logs** de ações do usuário, com opção de limpar.

## Stack

- .NET 10 + MAUI Blazor Hybrid (C#)
- [MudBlazor](https://mudblazor.com/) (componentes, tema claro/escuro, gráficos)
- SQLite + Entity Framework Core (migrações, padrão Repository)
- Plugin.LocalNotification (lembretes locais, canais com e sem som no Android)
- MAUI Essentials `MediaPicker` (câmera e galeria)
- Serilog (logs técnicos em arquivo local)

## Estrutura do projeto

```
OrcaFacil.slnx
├── OrcaFacil/              # App MAUI Blazor Hybrid (Android + Windows)
│   ├── Components/         # Layout, páginas, gráficos e componentes compartilhados (FabMenu)
│   ├── Services/           # Regras de negócio (lançamentos, fechamento, orçamento, metas, notificações, armazenamento, perfil)
│   ├── Data/               # Startup do banco (DbInitializer, SeedData, caminho do SQLite)
│   ├── Theme/              # Tema MudBlazor
│   └── Platforms/Android/  # Manifest (permissões), BootReceiver
├── OrcaFacil.Core/         # Entidades, DTOs, DbContext, migrações e repositórios (sem dependência de MAUI)
├── OrcaFacil.Tests/        # Testes xUnit com SQLite em memória (rodam sem emulador)
└── docs/                   # Planejamento (Fase 2: investimentos)
```

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download) ou superior
- Workload MAUI para Android:

```bash
dotnet workload install maui-android
```

- Emulador Android (API 24+) ou dispositivo físico com depuração USB.
- `dotnet-ef` (opcional, para gerar migrações):

```bash
dotnet tool install --global dotnet-ef
```

## Build e execução

```bash
# Android
dotnet build OrcaFacil/OrcaFacil.csproj -f net10.0-android

# Executar num emulador/dispositivo conectado
dotnet build OrcaFacil/OrcaFacil.csproj -f net10.0-android -t:Run

# Windows (útil para conferir o layout rapidamente; notificações e câmera têm suporte limitado)
dotnet build OrcaFacil/OrcaFacil.csproj -f net10.0-windows10.0.19041.0

# Testes
dotnet test OrcaFacil.Tests/OrcaFacil.Tests.csproj
```

## Permissões Android

| Permissão | Uso |
|---|---|
| `POST_NOTIFICATIONS`, `SCHEDULE_EXACT_ALARM`, `RECEIVE_BOOT_COMPLETED` | Lembretes de despesas e reagendamento após reiniciar o aparelho |
| `CAMERA` | Selfie para a foto do perfil |
| `READ_MEDIA_IMAGES` (Android 13+) / `READ_EXTERNAL_STORAGE` (até Android 12) | Escolher a foto do perfil na galeria |

## Banco de dados

O SQLite é criado em `FileSystem.AppDataDirectory` na primeira execução, com as migrações do EF Core aplicadas e dados de demonstração fictícios (`OrcaFacil/Data/SeedData.cs`). A opção *Configurações > Armazenamento > Limpar dados > Tudo* zera a aplicação e recria apenas as categorias padrão e um perfil vazio.

Para criar uma nova migração após alterar as entidades em `OrcaFacil.Core/Entities`:

```bash
dotnet ef migrations add NomeDaMigracao --project OrcaFacil.Core/OrcaFacil.Core.csproj --output-dir Data/Migrations
```

## Roadmap

- **Fase 2 — Investimentos**: acompanhamento de ações, ETFs, FIIs e criptos (preço médio, resultado, alocação, proventos e apuração de IR). Planejamento em [docs/fase2-investimentos.md](docs/fase2-investimentos.md).
- Backup/exportação do banco e sincronização opcional em nuvem (`ISyncService` já existe com implementação mock).
- Exportação de relatórios em PDF.

## Licença e autoria

Desenvolvido por **AZ Sistemas** — [Daniel Pitthan](https://github.com/DanielPitthan).
