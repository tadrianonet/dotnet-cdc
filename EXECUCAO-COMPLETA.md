# 🚀 Execução Completa - Pipeline CDC + Kafka + .NET

Este guia apresenta os passos para executar todo o pipeline de streaming de dados, desde o SQL Server com CDC até as aplicações .NET consumindo eventos do Kafka.

## 📋 Pré-requisitos

- Docker e Docker Compose instalados
- .NET 10 SDK instalado
- Git (opcional)

## 🎯 Arquitetura que Vamos Executar

```
SQL Server (CDC) → Debezium → Kafka → Aplicações .NET
     ↓              ↓         ↓           ↓
  Captura        Streaming  Message    Analytics
  Mudanças       CDC        Broker     Real-time
```

---

## 📚 ETAPA 1: Infraestrutura Completa

### 1.1 Limpar Ambiente (se necessário)
```bash
# Navegar para o diretório do projeto
cd /caminho/para/artigos

# Limpar ambiente anterior (se existir)
docker-compose -f docker-compose-debezium.yml down -v
docker stop sqlserver-cdc 2>/dev/null || true
docker rm sqlserver-cdc 2>/dev/null || true
```

### 1.2 Subir Infraestrutura Completa
```bash
# Subir todo o ambiente: SQL Server + Zookeeper + Kafka + Debezium + Kafka UI
docker-compose -f docker-compose-debezium.yml up -d

# Verificar se todos os serviços estão rodando
docker ps
```

**✅ Resultado esperado**: 5 containers rodando (sqlserver-cdc, zookeeper, kafka, connect, kafka-ui)

---

## 📚 ETAPA 2: Configurar SQL Server com CDC

### 2.1 Aguardar Inicialização
```bash
# Aguardar SQL Server inicializar completamente (30-60 segundos)
sleep 60
```

### 2.2 Configurar Banco e CDC
```bash
# Criar script SQL temporário
cat > /tmp/setup_cdc.sql << 'EOF'
-- Criar banco
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'EcommerceCDC')
BEGIN
    CREATE DATABASE EcommerceCDC;
END
GO

USE EcommerceCDC;
GO

-- Habilitar CDC no banco
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'EcommerceCDC' AND is_cdc_enabled = 1)
BEGIN
    EXEC sys.sp_cdc_enable_db;
END
GO

-- Criar tabela Produtos
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Produtos' AND xtype='U')
BEGIN
    CREATE TABLE Produtos (
        ProdutoID INT IDENTITY(1,1) PRIMARY KEY,
        Nome NVARCHAR(100) NOT NULL,
        Preco DECIMAL(10,2) NOT NULL,
        Estoque INT NOT NULL,
        Categoria NVARCHAR(50),
        DataCriacao DATETIME2 DEFAULT GETDATE(),
        DataAtualizacao DATETIME2 DEFAULT GETDATE()
    );
END
GO

-- Habilitar CDC na tabela
IF NOT EXISTS (SELECT * FROM cdc.change_tables WHERE source_object_id = OBJECT_ID('dbo.Produtos'))
BEGIN
    EXEC sys.sp_cdc_enable_table
        @source_schema = N'dbo',
        @source_name = N'Produtos',
        @role_name = NULL;
END
GO

-- Inserir dados iniciais
IF NOT EXISTS (SELECT * FROM Produtos)
BEGIN
    INSERT INTO Produtos (Nome, Preco, Estoque, Categoria) VALUES
    ('Smartphone Galaxy', 899.99, 50, 'Eletrônicos'),
    ('Notebook Dell', 1299.99, 25, 'Informática'),
    ('Fone Bluetooth', 199.99, 100, 'Acessórios'),
    ('Tablet iPad', 799.99, 30, 'Eletrônicos'),
    ('Mouse Gamer', 89.99, 200, 'Periféricos');
END
GO

-- Verificar configuração
SELECT name, is_cdc_enabled FROM sys.databases WHERE name = 'EcommerceCDC';
SELECT COUNT(*) as TotalProdutos FROM Produtos;
EOF

# Executar script no SQL Server
docker exec -i sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "MinhaSenh@123" -C < /tmp/setup_cdc.sql

# Limpar arquivo temporário
rm -f /tmp/setup_cdc.sql
```

**✅ Resultado esperado**: 
- CDC habilitado no banco EcommerceCDC
- Tabela Produtos criada com 5 registros
- SQL Server Agent rodando (jobs CDC criados)

---

### 3.1 Registrar Connector
```bash
# Aguardar Kafka Connect inicializar (60-90 segundos)
sleep 90

# Registrar o connector
curl -X POST localhost:8083/connectors \
  -H "Content-Type: application/json" \
  -d @sqlserver-connector.json

# Verificar status
curl localhost:8083/connectors/ecommerce-connector/status
```

**✅ Resultado esperado**: Connector status "RUNNING"

### 3.2 Validar Captura de Eventos
```bash
# Listar tópicos criados
docker exec kafka kafka-topics --bootstrap-server localhost:9092 --list

# Monitorar eventos em tempo real
docker exec kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic ecommerce.EcommerceCDC.dbo.Produtos \
  --property print.key=true
```

---

## 📚 ETAPA 4: Aplicações .NET

### 4.1 Preparar Ambiente .NET
```bash
# Verificar .NET instalado
dotnet --version

# Compilar todos os projetos
dotnet build KafkaProducer/
dotnet build KafkaConsumer/
dotnet build KafkaStreamProcessor/
```

### 4.2 Criar Tópicos para Aplicações
```bash
# Criar tópico para eventos de e-commerce
docker exec kafka kafka-topics --bootstrap-server localhost:9092 \
  --create --topic eventos-paralelos --partitions 3 --replication-factor 1 --if-not-exists

# Criar tópicos para stream processing
docker exec kafka kafka-topics --bootstrap-server localhost:9092 \
  --create --topic alertas-fraude --partitions 3 --replication-factor 1 --if-not-exists

docker exec kafka kafka-topics --bootstrap-server localhost:9092 \
  --create --topic compras-processadas --partitions 3 --replication-factor 1 --if-not-exists

docker exec kafka kafka-topics --bootstrap-server localhost:9092 \
  --create --topic recomendacoes --partitions 3 --replication-factor 1 --if-not-exists
```

---

## 🎬 EXECUÇÃO COMPLETA - DEMO

### Terminal 1: Producer (Gerador de Eventos)
```bash
cd KafkaProducer
dotnet run
```
**Resultado**: Gera 50 eventos de e-commerce (login, view_product, purchase, etc.)

### Terminal 2: Consumer (Analytics)
```bash
cd KafkaConsumer
dotnet run
```
**Resultado**: Processa eventos e mostra estatísticas em tempo real

### Terminal 3: Stream Processor (Pipeline Avançado)
```bash
cd KafkaStreamProcessor
dotnet run
```
**Resultado**: Detecta fraudes, processa compras, gera recomendações

### Terminal 4: Monitor Kafka UI
```bash
# Abrir no navegador
open http://localhost:8080
```
**Resultado**: Interface web para monitorar tópicos, partições, consumers

### Terminal 5: Monitor CDC (Debezium)
```bash
# Monitorar mudanças do banco em tempo real
docker exec kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic ecommerce.EcommerceCDC.dbo.Produtos \
  --property print.key=true
```

### Terminal 6: Inserir Dados no SQL Server
```bash
# Conectar ao SQL Server
docker exec -it sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "MinhaSenh@123" -C

# Inserir novos produtos (será capturado pelo CDC)
INSERT INTO EcommerceCDC.dbo.Produtos (Nome, Preco, Estoque, Categoria)
VALUES ('Produto Teste CDC', 299.99, 10, 'Teste');
```

---

## 📊 O que Você Verá

### 1. **CDC em Ação**
- Mudanças no SQL Server aparecem automaticamente no Kafka
- Debezium captura INSERT, UPDATE, DELETE em tempo real

### 2. **Producer .NET**
```
🚀 Iniciando produtor de eventos de e-commerce (.NET 10)...
📨 Evento 1: login - Usuário: user001 - Partição: 0 - Offset: 0
📨 Evento 2: view_product - Usuário: user002 - Partição: 1 - Offset: 0
```

### 3. **Consumer .NET**
```
📊 Evento processado: purchase - Usuário: user001 - Partição: 0 - Offset: 5
💰 COMPRA DETECTADA: user001 - Produto: produto_A - Valor: R$ 450,00

📈 ESTATÍSTICAS EM TEMPO REAL (.NET)
==================================================
Total de eventos: 25
Valor total: R$ 5.247,50
Sessões ativas: 8
```

### 4. **Stream Processor .NET**
```
🚨 ALERTA DE FRAUDE: user003 - Valor: R$ 489,99
💰 Compra processada: user001 - R$ 299,99
🎯 Recomendação gerada para: user002
```

### 5. **Kafka UI**
- Visualização de tópicos, partições, offsets
- Monitoramento de consumers e lag
- Métricas de performance

---

## 🔧 Comandos Úteis

### Parar Tudo
```bash
# Parar aplicações .NET (Ctrl+C em cada terminal)
# Parar Docker Compose
docker-compose -f docker-compose-debezium.yml down

# Limpar volumes (opcional)
docker-compose -f docker-compose-debezium.yml down -v
```

### Verificar Status
```bash
# Status dos containers
docker ps

# Logs do Debezium
docker logs connect

# Status do connector
curl localhost:8083/connectors/ecommerce-connector/status

# Listar tópicos
docker exec kafka kafka-topics --bootstrap-server localhost:9092 --list
```

### Reset Completo
```bash
# Parar e remover tudo
docker-compose -f docker-compose-debezium.yml down -v
docker system prune -f

# Limpar projetos .NET
dotnet clean KafkaProducer/
dotnet clean KafkaConsumer/
dotnet clean KafkaStreamProcessor/
```

---

## 🚨 Troubleshooting

### Problema: Connector falha
```bash
# Verificar logs
docker logs connect

# Recriar connector
curl -X DELETE localhost:8083/connectors/ecommerce-connector
curl -X POST localhost:8083/connectors -H "Content-Type: application/json" -d @sqlserver-connector.json
```

### Problema: .NET não conecta
```bash
# Verificar se Kafka está acessível
docker exec kafka kafka-broker-api-versions --bootstrap-server localhost:9092

# Verificar portas
netstat -an | grep 9092
```

### Problema: CDC não funciona
```bash
# Verificar se SQL Server Agent está rodando
docker exec sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "MinhaSenh@123" -C -Q "SELECT @@SERVERNAME, @@VERSION"

# Verificar se CDC está habilitado
docker exec sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "MinhaSenh@123" -C -Q "SELECT name, is_cdc_enabled FROM sys.databases WHERE name = 'EcommerceCDC'"
```

### Problema: Porta já em uso
```bash
# Verificar processos usando portas
lsof -i :1433  # SQL Server
lsof -i :2181  # Zookeeper
lsof -i :9092  # Kafka
lsof -i :8083  # Kafka Connect
lsof -i :8080  # Kafka UI

# Parar containers conflitantes
docker stop $(docker ps -q)
```

---

## 🎓 Fluxo de Aprendizado Recomendado

1. **Primeiro**: Execute apenas CDC + Debezium (Terminais 5 e 6)
2. **Segundo**: Adicione Producer + Consumer (Terminais 1 e 2)
3. **Terceiro**: Complete com Stream Processor (Terminal 3)
4. **Quarto**: Use Kafka UI para visualizar (Terminal 4)

---

## ✅ Resultado Final

Ao final, você terá:
- ✅ Pipeline completo de streaming de dados
- ✅ CDC capturando mudanças do banco em tempo real
- ✅ Kafka processando milhares de eventos por segundo
- ✅ Aplicações .NET fazendo analytics, detecção de fraude e recomendações
- ✅ Monitoramento visual via Kafka UI
- ✅ Arquitetura escalável e resiliente

**🎉 Parabéns! Você implementou uma arquitetura de streaming de dados completa!**

---

## 🚀 Execução Rápida (Alternativa)

Se preferir execução automatizada, use:
```bash
./executar-demo.sh
```

Ou consulte o guia rápido: `COMO-EXECUTAR.md`
