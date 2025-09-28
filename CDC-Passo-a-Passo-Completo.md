# Change Data Capture (CDC) no SQL Server: Guia Prático Completo

Este guia apresenta um passo a passo completo para implementar Change Data Capture (CDC) no SQL Server usando Docker, ideal para demonstrações em aula.

## Pré-requisitos

- Docker Desktop instalado
- Pelo menos 4GB de RAM disponível
- 10GB de espaço em disco livre
- Azure Data Studio ou SQL Server Management Studio (SSMS)

---

## PARTE 1: Configuração do Ambiente com Docker

### Passo 1: Criar container SQL Server

Execute este comando no terminal para criar um container SQL Server com todas as configurações necessárias para CDC:

```bash
# Criar e executar container SQL Server
docker run -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=MinhaSenh@123" \
  -e "MSSQL_AGENT_ENABLED=true" \
  -p 1433:1433 \
  --name sqlserver-cdc \
  --hostname sqlserver-cdc \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

**Explicação dos parâmetros:**
- `ACCEPT_EULA=Y`: aceita os termos de uso do SQL Server
- `MSSQL_SA_PASSWORD`: define a senha do usuário SA (deve ser complexa)
- `MSSQL_AGENT_ENABLED=true`: **ESSENCIAL para CDC** - habilita o SQL Server Agent
- `-p 1433:1433`: mapeia a porta padrão do SQL Server
- `--name sqlserver-cdc`: nome do container para facilitar gerenciamento
- `-d`: executa em background (detached mode)

### Passo 2: Verificar se o container está funcionando

```bash
# Verificar status do container
docker ps

# Verificar logs do SQL Server (aguarde aparecer "SQL Server is now ready for client connections")
docker logs sqlserver-cdc
```

**O que esperar:** Você deve ver o container listado como "Up" e nos logs a mensagem indicando que o SQL Server está pronto.

### Passo 3: Conectar ao SQL Server

**Usando Azure Data Studio ou SSMS:**
```
Server: localhost,1433
Authentication: SQL Login
User: sa
Password: MinhaSenh@123
```

**Testando a conexão via terminal:**
```bash
# Conectar via sqlcmd dentro do container
docker exec -it sqlserver-cdc /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P "MinhaSenh@123"
```

---

## PARTE 2: Implementação do CDC

### Passo 4: Criar e configurar o banco de dados

Execute estes comandos SQL no Azure Data Studio/SSMS:

```sql
-- Criar banco de dados para demonstração
CREATE DATABASE EcommerceCDC;
GO

USE EcommerceCDC;
GO

-- Verificar se o SQL Server Agent está ativo (ESSENCIAL para CDC)
SELECT 
    servicename,
    status_desc
FROM sys.dm_server_services
WHERE servicename LIKE '%Agent%';
```

**O que esperar:** O SQL Server Agent deve aparecer com status "Running".

```sql
-- Verificar se a edição suporta CDC
SELECT 
    SERVERPROPERTY('ProductVersion') AS Version,
    SERVERPROPERTY('ProductLevel') AS ServicePack,
    SERVERPROPERTY('Edition') AS Edition;
```

**O que esperar:** A edição deve ser Developer, Standard ou Enterprise.

### Passo 5: Criar estrutura de dados (cenário e-commerce)

```sql
-- Criar tabela de produtos
CREATE TABLE Produtos (
    ProdutoID INT IDENTITY(1,1) PRIMARY KEY,
    Nome NVARCHAR(100) NOT NULL,
    Preco DECIMAL(10,2) NOT NULL,
    Estoque INT NOT NULL,
    Categoria NVARCHAR(50),
    DataCriacao DATETIME2 DEFAULT GETDATE(),
    DataAtualizacao DATETIME2 DEFAULT GETDATE()
);

-- Inserir dados iniciais
INSERT INTO Produtos (Nome, Preco, Estoque, Categoria) VALUES
('Notebook Dell XPS', 3500.00, 10, 'Eletrônicos'),
('Mouse Logitech', 89.90, 50, 'Periféricos'),
('Teclado Mecânico', 299.99, 25, 'Periféricos'),
('Monitor 24"', 899.00, 15, 'Monitores'),
('SSD 1TB', 450.00, 30, 'Armazenamento');
```

```sql
-- Criar tabela de pedidos
CREATE TABLE Pedidos (
    PedidoID INT IDENTITY(1,1) PRIMARY KEY,
    ClienteID INT NOT NULL,
    ProdutoID INT NOT NULL,
    Quantidade INT NOT NULL,
    ValorTotal DECIMAL(10,2) NOT NULL,
    Status NVARCHAR(20) DEFAULT 'Pendente',
    DataPedido DATETIME2 DEFAULT GETDATE(),
    FOREIGN KEY (ProdutoID) REFERENCES Produtos(ProdutoID)
);

-- Inserir pedidos iniciais
INSERT INTO Pedidos (ClienteID, ProdutoID, Quantidade, ValorTotal, Status) VALUES
(1001, 1, 1, 3500.00, 'Confirmado'),
(1002, 2, 2, 179.80, 'Pendente'),
(1003, 3, 1, 299.99, 'Enviado');
```

**Explicação:** Criamos um cenário realista de e-commerce com produtos e pedidos para demonstrar o CDC em ação.

### Passo 6: Habilitar CDC

```sql
-- Verificar se CDC já está habilitado no banco
SELECT 
    name,
    is_cdc_enabled
FROM sys.databases
WHERE name = 'EcommerceCDC';
```

**O que esperar:** `is_cdc_enabled` deve ser 0 (falso) inicialmente.

```sql
-- Habilitar CDC no banco de dados
EXEC sys.sp_cdc_enable_db;
GO

-- Verificar se foi habilitado
SELECT 
    name,
    is_cdc_enabled
FROM sys.databases
WHERE name = 'EcommerceCDC';
```

**O que esperar:** Agora `is_cdc_enabled` deve ser 1 (verdadeiro).

```sql
-- Habilitar CDC na tabela Produtos
EXEC sys.sp_cdc_enable_table
    @source_schema = N'dbo',
    @source_name = N'Produtos',
    @role_name = NULL,
    @supports_net_changes = 1;

-- Habilitar CDC na tabela Pedidos
EXEC sys.sp_cdc_enable_table
    @source_schema = N'dbo',
    @source_name = N'Pedidos',
    @role_name = NULL,
    @supports_net_changes = 1;
```

**Explicação dos parâmetros:**
- `@source_schema`: esquema da tabela (geralmente 'dbo')
- `@source_name`: nome da tabela
- `@role_name`: role para controle de acesso (NULL = sem restrição)
- `@supports_net_changes`: habilita consultas de mudanças líquidas (requer chave primária)

### Passo 7: Verificar configuração CDC

```sql
-- Verificar tabelas com CDC habilitado
SELECT 
    s.name AS schema_name,
    t.name AS table_name,
    t.is_tracked_by_cdc
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.is_tracked_by_cdc = 1;
```

**O que esperar:** Deve mostrar as tabelas Produtos e Pedidos com `is_tracked_by_cdc = 1`.

```sql
-- Verificar change tables criadas automaticamente
SELECT 
    name,
    type_desc
FROM sys.objects
WHERE name LIKE 'cdc.dbo_%'
ORDER BY name;
```

**O que esperar:** Deve mostrar tabelas como `cdc.dbo_Produtos_CT` e `cdc.dbo_Pedidos_CT`.

---

## PARTE 3: Gerando e Capturando Mudanças

### Passo 8: Gerar mudanças para demonstração

**Operações de INSERT:**
```sql
-- Inserir novos produtos
INSERT INTO Produtos (Nome, Preco, Estoque, Categoria) VALUES
('Webcam HD', 199.90, 20, 'Periféricos'),
('Headset Gamer', 350.00, 12, 'Áudio');

-- Inserir novos pedidos
INSERT INTO Pedidos (ClienteID, ProdutoID, Quantidade, ValorTotal, Status) VALUES
(1004, 4, 1, 899.00, 'Confirmado'),
(1005, 6, 1, 199.90, 'Pendente');
```

**Operações de UPDATE:**
```sql
-- Atualizar preços de produtos
UPDATE Produtos 
SET Preco = 3200.00, DataAtualizacao = GETDATE()
WHERE ProdutoID = 1;

UPDATE Produtos 
SET Estoque = Estoque - 5, DataAtualizacao = GETDATE()
WHERE ProdutoID = 2;

-- Atualizar status de pedidos
UPDATE Pedidos 
SET Status = 'Enviado'
WHERE PedidoID IN (2, 4);
```

**Operações de DELETE:**
```sql
-- Deletar um produto descontinuado
DELETE FROM Pedidos WHERE ProdutoID = 5; -- Remover pedidos relacionados primeiro
DELETE FROM Produtos WHERE ProdutoID = 5;
```

**Explicação:** Essas operações simulam atividades reais de um e-commerce: novos produtos, atualizações de preço/estoque, mudanças de status e remoções.

### Passo 9: Consultar mudanças capturadas

**Verificar LSNs disponíveis:**
```sql
-- Script para verificar LSNs de ambas as tabelas
SELECT 
    'Produtos' AS Tabela,
    sys.fn_cdc_get_min_lsn('dbo_Produtos') AS LSN_Minimo,
    sys.fn_cdc_get_max_lsn() AS LSN_Maximo
UNION ALL
SELECT 
    'Pedidos' AS Tabela,
    sys.fn_cdc_get_min_lsn('dbo_Pedidos') AS LSN_Minimo,
    sys.fn_cdc_get_max_lsn() AS LSN_Maximo;
```

**Explicação:** LSN (Log Sequence Number) marca pontos no tempo. O CDC captura mudanças entre dois LSNs.

**Consultar TODAS as mudanças (recomendado para demonstração):**
```sql
-- Consultar todas as mudanças em Produtos
SELECT 
    CASE __$operation
        WHEN 1 THEN 'DELETE'
        WHEN 2 THEN 'INSERT'
        WHEN 3 THEN 'UPDATE (antes)'
        WHEN 4 THEN 'UPDATE (depois)'
    END AS Operacao,
    __$start_lsn AS LSN_Transacao,
    __$update_mask AS Colunas_Alteradas,
    ProdutoID,
    Nome,
    Preco,
    Estoque,
    Categoria,
    DataCriacao,
    DataAtualizacao
FROM cdc.fn_cdc_get_all_changes_dbo_Produtos(
    sys.fn_cdc_get_min_lsn('dbo_Produtos'), 
    sys.fn_cdc_get_max_lsn(), 
    N'all'
)
ORDER BY __$start_lsn;
```

**O que esperar:**
- Registros com `Operacao = 'INSERT'` para novos produtos
- Registros com `Operacao = 'UPDATE (antes)'` e `'UPDATE (depois)'` para alterações
- Registros com `Operacao = 'DELETE'` para remoções

**Consultar mudanças LÍQUIDAS (resultado final):**
```sql
-- Consultar mudanças líquidas em Produtos
SELECT 
    CASE __$operation
        WHEN 1 THEN 'DELETE'
        WHEN 2 THEN 'INSERT'
        WHEN 4 THEN 'UPDATE'
        WHEN 5 THEN 'MERGE'
    END AS Operacao,
    __$start_lsn AS LSN_Transacao,
    ProdutoID,
    Nome,
    Preco,
    Estoque,
    Categoria
FROM cdc.fn_cdc_get_net_changes_dbo_Produtos(
    sys.fn_cdc_get_min_lsn('dbo_Produtos'), 
    sys.fn_cdc_get_max_lsn(), 
    N'all'
)
ORDER BY __$start_lsn;
```

**Diferença entre All Changes e Net Changes:**
- **All Changes**: mostra cada operação individual (útil para auditoria)
- **Net Changes**: mostra apenas o resultado final (útil para sincronização)

---

## PARTE 4: Criando um Consumidor de Mudanças

### Passo 10: Criar sistema de checkpoint

```sql
-- Criar tabela para controlar o progresso do consumidor
CREATE TABLE CDC_Checkpoint (
    Tabela NVARCHAR(100) PRIMARY KEY,
    Ultimo_LSN BINARY(10) NOT NULL,
    Data_Processamento DATETIME2 DEFAULT GETDATE()
);

-- Inserir checkpoints iniciais
INSERT INTO CDC_Checkpoint (Tabela, Ultimo_LSN) VALUES
('dbo_Produtos', sys.fn_cdc_get_min_lsn('dbo_Produtos')),
('dbo_Pedidos', sys.fn_cdc_get_min_lsn('dbo_Pedidos'));
```

**Explicação:** O checkpoint permite que o consumidor saiba onde parou, evitando reprocessar mudanças já tratadas.

### Passo 11: Criar procedimento consumidor

```sql
CREATE PROCEDURE sp_ProcessarMudancasProdutos
AS
BEGIN
    DECLARE @from_lsn BINARY(10);
    DECLARE @to_lsn BINARY(10);
    
    -- Obter último LSN processado
    SELECT @from_lsn = Ultimo_LSN 
    FROM CDC_Checkpoint 
    WHERE Tabela = 'dbo_Produtos';
    
    -- Obter LSN atual
    SET @to_lsn = sys.fn_cdc_get_max_lsn();
    
    -- Verificar se há mudanças para processar
    IF @to_lsn > @from_lsn
    BEGIN
        -- Processar mudanças
        SELECT 
            'Processando mudança' AS Status,
            CASE __$operation
                WHEN 1 THEN 'DELETE'
                WHEN 2 THEN 'INSERT'
                WHEN 3 THEN 'UPDATE (antes)'
                WHEN 4 THEN 'UPDATE (depois)'
            END AS Operacao,
            ProdutoID,
            Nome,
            Preco,
            Estoque
        FROM cdc.fn_cdc_get_all_changes_dbo_Produtos(@from_lsn, @to_lsn, N'all')
        ORDER BY __$start_lsn;
        
        -- Atualizar checkpoint
        UPDATE CDC_Checkpoint 
        SET Ultimo_LSN = @to_lsn, 
            Data_Processamento = GETDATE()
        WHERE Tabela = 'dbo_Produtos';
        
        PRINT 'Mudanças processadas com sucesso!';
    END
    ELSE
    BEGIN
        PRINT 'Nenhuma mudança encontrada.';
    END
END;
```

**Explicação:** Este procedimento simula um consumidor real que:
1. Verifica o último LSN processado
2. Busca novas mudanças
3. Processa as mudanças encontradas
4. Atualiza o checkpoint

### Passo 12: Testar o consumidor

```sql
-- Executar o processamento de mudanças
EXEC sp_ProcessarMudancasProdutos;

-- Verificar checkpoint atualizado
SELECT * FROM CDC_Checkpoint;
```

**Para testar novamente:**
1. Execute mais operações INSERT/UPDATE/DELETE
2. Execute novamente `EXEC sp_ProcessarMudancasProdutos;`
3. Observe que apenas as novas mudanças são processadas

---

## PARTE 5: Monitoramento e Manutenção

### Passo 13: Monitorar jobs do CDC

```sql
-- Verificar jobs do SQL Server Agent relacionados ao CDC
SELECT 
    j.name AS job_name,
    j.enabled,
    ja.run_requested_date,
    ja.last_executed_step_date,
    ja.last_execution_outcome
FROM msdb.dbo.sysjobs j
LEFT JOIN msdb.dbo.sysjobactivity ja ON j.job_id = ja.job_id
WHERE j.name LIKE '%cdc%'
ORDER BY j.name;
```

**O que esperar:** Deve mostrar jobs como:
- `cdc.EcommerceCDC_capture` (captura mudanças)
- `cdc.EcommerceCDC_cleanup` (limpeza de dados antigos)

### Passo 14: Monitorar espaço usado

```sql
-- Verificar tamanho das change tables
SELECT 
    t.name AS table_name,
    p.rows AS row_count,
    (p.used_page_count * 8) / 1024 AS used_space_mb
FROM sys.tables t
INNER JOIN sys.dm_db_partition_stats p ON t.object_id = p.object_id
WHERE t.name LIKE 'dbo_%_CT'
ORDER BY used_space_mb DESC;
```

### Passo 15: Configurar retenção

```sql
-- Verificar configuração atual de retenção
SELECT 
    retention,
    pollinginterval
FROM msdb.dbo.cdc_jobs
WHERE job_type = 'cleanup';

-- Alterar período de retenção para 3 dias (4320 minutos)
EXEC sys.sp_cdc_change_job 
    @job_type = N'cleanup',
    @retention = 4320;
```

**Explicação:** A retenção controla por quanto tempo as mudanças ficam disponíveis. Muito tempo = mais espaço usado; pouco tempo = risco de perder mudanças não processadas.

---

## PARTE 6: Comandos Úteis para Gerenciamento

### Gerenciar o container Docker

```bash
# Parar container
docker stop sqlserver-cdc

# Iniciar container
docker start sqlserver-cdc

# Reiniciar container
docker restart sqlserver-cdc

# Ver logs em tempo real
docker logs -f sqlserver-cdc

# Remover container (cuidado: perde todos os dados)
docker rm sqlserver-cdc
```

### Fazer backup do banco

```bash
# Fazer backup do banco
docker exec sqlserver-cdc /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P "MinhaSenh@123" \
  -Q "BACKUP DATABASE EcommerceCDC TO DISK = '/tmp/EcommerceCDC.bak'"

# Copiar backup para o host
docker cp sqlserver-cdc:/tmp/EcommerceCDC.bak ./EcommerceCDC.bak
```

---

## Troubleshooting

### Problema: SQL Server Agent não está ativo
```sql
-- Verificar status
EXEC xp_servicecontrol 'QueryState', 'SQLServerAGENT';
```
**Solução:** Recriar container com `MSSQL_AGENT_ENABLED=true`

### Problema: Erro de memória
```bash
# Aumentar memória do container
docker run -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=MinhaSenh@123" \
  -e "MSSQL_AGENT_ENABLED=true" \
  --memory="4g" \
  -p 1433:1433 \
  --name sqlserver-cdc \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

### Problema: Porta já em uso
```bash
# Usar porta diferente
docker run -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=MinhaSenh@123" \
  -e "MSSQL_AGENT_ENABLED=true" \
  -p 1434:1433 \
  --name sqlserver-cdc \
  -d mcr.microsoft.com/mssql/server:2022-latest

# Conectar usando: localhost,1434
```
