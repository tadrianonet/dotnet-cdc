#!/bin/bash

# 🚀 Script de Execução Automática - Pipeline CDC + Kafka + .NET
# Autor: Assistente IA
# Descrição: Executa todo o pipeline de streaming de dados automaticamente

set -e  # Parar em caso de erro

echo "🚀 Iniciando Pipeline CDC + Kafka + .NET..."
echo "=============================================="

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Função para imprimir com cor
print_step() {
    echo -e "${BLUE}📋 $1${NC}"
}

print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

# Verificar pré-requisitos
print_step "Verificando pré-requisitos..."

if ! command -v docker &> /dev/null; then
    print_error "Docker não encontrado. Instale o Docker primeiro."
    exit 1
fi

if ! command -v docker-compose &> /dev/null; then
    print_error "Docker Compose não encontrado. Instale o Docker Compose primeiro."
    exit 1
fi

if ! command -v dotnet &> /dev/null; then
    print_error ".NET SDK não encontrado. Instale o .NET 10 SDK primeiro."
    exit 1
fi

print_success "Pré-requisitos OK"

# Limpar ambiente anterior
print_step "Limpando ambiente anterior..."
docker-compose -f docker-compose-debezium.yml down -v 2>/dev/null || true
docker stop sqlserver-cdc 2>/dev/null || true
docker rm sqlserver-cdc 2>/dev/null || true
print_success "Ambiente limpo"

# Subir infraestrutura
print_step "Subindo infraestrutura Kafka + Debezium..."
docker-compose -f docker-compose-debezium.yml up -d

print_step "Aguardando serviços iniciarem (90 segundos)..."
sleep 90

# Verificar se serviços estão rodando
print_step "Verificando serviços..."
if ! docker ps | grep -q "kafka"; then
    print_error "Kafka não está rodando"
    exit 1
fi

if ! docker ps | grep -q "sqlserver-cdc"; then
    print_error "SQL Server não está rodando"
    exit 1
fi

print_success "Todos os serviços estão rodando"

# Compilar projetos .NET
print_step "Compilando projetos .NET..."
dotnet build KafkaProducer/ --verbosity quiet
dotnet build KafkaConsumer/ --verbosity quiet  
dotnet build KafkaStreamProcessor/ --verbosity quiet
print_success "Projetos .NET compilados"

# Criar tópicos
print_step "Criando tópicos Kafka..."
docker exec kafka kafka-topics --bootstrap-server localhost:9092 \
  --create --topic eventos-paralelos --partitions 3 --replication-factor 1 \
  --if-not-exists 2>/dev/null || true

docker exec kafka kafka-topics --bootstrap-server localhost:9092 \
  --create --topic alertas-fraude --partitions 3 --replication-factor 1 \
  --if-not-exists 2>/dev/null || true

docker exec kafka kafka-topics --bootstrap-server localhost:9092 \
  --create --topic compras-processadas --partitions 3 --replication-factor 1 \
  --if-not-exists 2>/dev/null || true

docker exec kafka kafka-topics --bootstrap-server localhost:9092 \
  --create --topic recomendacoes --partitions 3 --replication-factor 1 \
  --if-not-exists 2>/dev/null || true

print_success "Tópicos criados"

# Configurar banco de dados
print_step "Configurando banco de dados SQL Server..."
sleep 10  # Aguardar SQL Server estar pronto

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
EOF

# Executar script no SQL Server
docker exec -i sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "MinhaSenh@123" -C < /tmp/setup_cdc.sql

print_success "Banco de dados configurado"

# Aguardar Kafka Connect
print_step "Aguardando Kafka Connect (60 segundos)..."
sleep 60

# Registrar Debezium connector
print_step "Registrando Debezium connector..."
curl -s -X POST localhost:8083/connectors \
  -H "Content-Type: application/json" \
  -d @sqlserver-connector.json > /dev/null

sleep 10

# Verificar status do connector
CONNECTOR_STATUS=$(curl -s localhost:8083/connectors/ecommerce-connector/status | grep -o '"state":"[^"]*"' | cut -d'"' -f4)
if [ "$CONNECTOR_STATUS" = "RUNNING" ]; then
    print_success "Debezium connector configurado e rodando"
else
    print_warning "Connector status: $CONNECTOR_STATUS"
fi

# Limpar arquivo temporário
rm -f /tmp/setup_cdc.sql

echo ""
echo "🎉 PIPELINE CONFIGURADO COM SUCESSO!"
echo "====================================="
echo ""
echo "📋 Próximos passos:"
echo ""
echo "1️⃣  Abrir Kafka UI:"
echo "   http://localhost:8080"
echo ""
echo "2️⃣  Executar Producer (Terminal 1):"
echo "   cd KafkaProducer && dotnet run"
echo ""
echo "3️⃣  Executar Consumer (Terminal 2):"
echo "   cd KafkaConsumer && dotnet run"
echo ""
echo "4️⃣  Executar Stream Processor (Terminal 3):"
echo "   cd KafkaStreamProcessor && dotnet run"
echo ""
echo "5️⃣  Monitorar CDC (Terminal 4):"
echo "   docker exec kafka kafka-console-consumer \\"
echo "     --bootstrap-server localhost:9092 \\"
echo "     --topic ecommerce.EcommerceCDC.dbo.Produtos \\"
echo "     --property print.key=true"
echo ""
echo "6️⃣  Testar CDC inserindo dados (Terminal 5):"
echo "   docker exec -it sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd \\"
echo "     -S localhost -U sa -P \"MinhaSenh@123\" -C"
echo ""
echo "   Depois execute:"
echo "   USE EcommerceCDC;"
echo "   INSERT INTO Produtos (Nome, Preco, Estoque, Categoria)"
echo "   VALUES ('Produto CDC Teste', 299.99, 15, 'Teste');"
echo ""
echo "🔧 Para parar tudo:"
echo "   docker-compose -f docker-compose-debezium.yml down -v"
echo ""
echo "📖 Consulte EXECUCAO-COMPLETA.md para detalhes completos"
echo ""
print_success "Setup concluído! Bom streaming! 🚀"
