# 🚀 Como Executar - Resumo Executivo

## ⚡ Execução Rápida (Automatizada)

```bash
# 1. Executar script automático
./executar-demo.sh

# 2. Aguardar conclusão (3-4 minutos)

# 3. Abrir terminais e executar:
```

### Terminal 1 - Producer
```bash
cd KafkaProducer
dotnet run
```

### Terminal 2 - Consumer  
```bash
cd KafkaConsumer
dotnet run
```

### Terminal 3 - Stream Processor
```bash
cd KafkaStreamProcessor
dotnet run
```

### Terminal 4 - Monitor CDC
```bash
docker exec kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic ecommerce.EcommerceCDC.dbo.Produtos \
  --property print.key=true
```

### Terminal 5 - Testar CDC
```bash
docker exec -it sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "MinhaSenh@123" -C

# Depois:
USE EcommerceCDC;
INSERT INTO Produtos (Nome, Preco, Estoque, Categoria)
VALUES ('Produto Teste', 299.99, 10, 'Teste');
```

## 🌐 Interfaces Web

- **Kafka UI**: http://localhost:8080

## 🛑 Parar Tudo

```bash
# Parar aplicações: Ctrl+C em cada terminal
# Parar infraestrutura:
docker-compose -f docker-compose-debezium.yml down -v
```

## 📖 Documentação Completa

- **Detalhado**: `EXECUCAO-COMPLETA.md`
- **Guias individuais**: `CDC-Passo-a-Passo-Completo.md`, `Debezium-Passo-a-Passo.md`, etc.
- **Diagramas**: pasta `docs/`

---

**🎯 Em 5 minutos você terá um pipeline completo de streaming de dados rodando!**
