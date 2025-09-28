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

## 📚 ETAPA 1: SQL Server com CDC

### 1.1 Subir SQL Server
```bash
# Navegar para o diretório do projeto
cd /caminho/para/artigos

# Subir SQL Server com CDC
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=MinhaSenh@123" \
  -p 1433:1433 --name sqlserver-cdc \
  -d mcr.microsoft.com/mssql/server:2019-latest
```

### 1.2 Configurar Banco e CDC
```bash
# Conectar ao SQL Server
docker exec -it sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "MinhaSenh@123" -C

# Executar os comandos SQL do arquivo CDC-Passo-a-Passo-Completo.md
# (Criar banco, tabelas, habilitar CDC, inserir dados)
```

**⚠️ Importante**: Execute todos os scripts SQL do arquivo `CDC-Passo-a-Passo-Completo.md` antes de prosseguir.

---

## 📚 ETAPA 2: Debezium + Kafka

### 2.1 Subir Infraestrutura Kafka
```bash
# Parar SQL Server temporariamente (será incluído no docker-compose)
docker stop sqlserver-cdc
docker rm sqlserver-cdc

# Subir todo o ambiente Debezium
docker-compose -f docker-compose-debezium.yml up -d

# Verificar se todos os serviços estão rodando
docker ps
```

### 2.2 Recriar Banco no Novo Container
```bash
# Conectar ao novo SQL Server
docker exec -it sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "MinhaSenh@123" -C

# Reexecutar scripts de criação do banco e CDC
# (Mesmo processo da etapa 1.2)
```

### 2.3 Configurar Debezium Connector
```bash
# Aguardar Kafka Connect inicializar (2-3 minutos)
sleep 180

# Registrar o connector
curl -X POST localhost:8083/connectors \
  -H "Content-Type: application/json" \
  -d @sqlserver-connector.json

# Verificar status
curl localhost:8083/connectors/ecommerce-connector/status
```

### 2.4 Validar Captura de Eventos
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

## 📚 ETAPA 3: Aplicações .NET

### 3.1 Preparar Ambiente .NET
```bash
# Verificar .NET instalado
dotnet --version

# Compilar todos os projetos
dotnet build KafkaProducer/
dotnet build KafkaConsumer/
dotnet build KafkaStreamProcessor/
```

### 3.2 Criar Tópicos para Aplicações
```bash
# Criar tópico para eventos de e-commerce
docker exec kafka kafka-topics --bootstrap-server localhost:9092 \
  --create --topic eventos-paralelos --partitions 3 --replication-factor 1

# Criar tópicos para stream processing
docker exec kafka kafka-topics --bootstrap-server localhost:9092 \
  --create --topic alertas-fraude --partitions 3 --replication-factor 1

docker exec kafka kafka-topics --bootstrap-server localhost:9092 \
  --create --topic compras-processadas --partitions 3 --replication-factor 1

docker exec kafka kafka-topics --bootstrap-server localhost:9092 \
  --create --topic recomendacoes --partitions 3 --replication-factor 1
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
