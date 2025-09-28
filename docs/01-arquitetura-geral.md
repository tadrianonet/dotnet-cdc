# Arquitetura Geral: CDC → Debezium → Zookeeper → Kafka

## Visão Macro da Solução

Este diagrama apresenta a arquitetura completa da solução de streaming de dados implementada.

```mermaid
graph TB
    subgraph "Camada de Dados"
        DB[(SQL Server<br/>EcommerceCDC)]
        CDC[CDC Tables<br/>cdc.dbo_Produtos<br/>cdc.dbo_Pedidos]
        DB --> CDC
    end

    subgraph "Camada de Streaming"
        DEB[Debezium Connector<br/>ecommerce-connector]
        ZK1[Zookeeper 1<br/>:2181]
        ZK2[Zookeeper 2<br/>:2182] 
        ZK3[Zookeeper 3<br/>:2183]
        
        K1[Kafka Broker 1<br/>:9092]
        K2[Kafka Broker 2<br/>:9093]
        K3[Kafka Broker 3<br/>:9094]
        
        KC[Kafka Connect<br/>:8083]
        KUI[Kafka UI<br/>:8080]
    end

    subgraph "Camada de Aplicação"
        PROD[.NET Producer<br/>KafkaProducer]
        CONS[.NET Consumer<br/>KafkaConsumer]
        STREAM[.NET Stream Processor<br/>KafkaStreamProcessor]
    end

    subgraph "Tópicos Kafka"
        T1[ecommerce.EcommerceCDC.dbo.Produtos]
        T2[ecommerce.EcommerceCDC.dbo.Pedidos]
        T3[eventos-paralelos]
        T4[alertas-fraude]
        T5[compras-processadas]
        T6[recomendacoes]
    end

    %% Conexões CDC
    CDC --> DEB
    DEB --> KC
    KC --> T1
    KC --> T2

    %% Coordenação Zookeeper
    ZK1 -.-> K1
    ZK2 -.-> K2
    ZK3 -.-> K3
    ZK1 -.-> ZK2
    ZK2 -.-> ZK3
    ZK3 -.-> ZK1

    %% Cluster Kafka
    K1 -.-> K2
    K2 -.-> K3
    K3 -.-> K1

    %% Aplicações
    PROD --> T3
    T3 --> CONS
    T3 --> STREAM
    STREAM --> T4
    STREAM --> T5
    STREAM --> T6

    %% Monitoramento
    KUI --> K1
    KUI --> K2
    KUI --> K3

    %% Estilos
    classDef database fill:#e1f5fe
    classDef streaming fill:#f3e5f5
    classDef application fill:#e8f5e8
    classDef topics fill:#fff3e0

    class DB,CDC database
    class DEB,ZK1,ZK2,ZK3,K1,K2,K3,KC,KUI streaming
    class PROD,CONS,STREAM application
    class T1,T2,T3,T4,T5,T6 topics
```

## Componentes da Arquitetura

### 🗄️ **Camada de Dados**
- **SQL Server**: Banco principal com CDC habilitado
- **CDC Tables**: Tabelas de captura de mudanças automáticas

### 🔄 **Camada de Streaming**
- **Debezium Connector**: Captura mudanças do CDC e envia para Kafka
- **Zookeeper Ensemble**: Coordenação distribuída (3 nós para alta disponibilidade)
- **Kafka Cluster**: Processamento distribuído (3 brokers)
- **Kafka Connect**: Infraestrutura para conectores
- **Kafka UI**: Interface web para monitoramento

### 💻 **Camada de Aplicação**
- **.NET Producer**: Gera eventos de e-commerce
- **.NET Consumer**: Processa eventos com analytics
- **.NET Stream Processor**: Pipeline de processamento em tempo real

### 📊 **Tópicos Kafka**
- **CDC Topics**: Mudanças capturadas do banco
- **Application Topics**: Eventos de aplicação
- **Processing Topics**: Resultados do processamento

## Fluxo de Dados

### 1. **Captura (CDC)**
```
SQL Server → CDC Tables → Debezium → Kafka Topics
```

### 2. **Processamento (Streaming)**
```
Kafka Topics → .NET Applications → Processed Topics
```

### 3. **Coordenação (Zookeeper)**
```
Zookeeper Ensemble ↔ Kafka Cluster
```

## Características da Arquitetura

### ✅ **Alta Disponibilidade**
- Zookeeper: 3 nós (tolerância a 1 falha)
- Kafka: 3 brokers (replicação factor 3)
- Debezium: Reconexão automática

### ⚡ **Performance**
- Particionamento por chave (user_id)
- Processamento paralelo
- Replicação assíncrona

### 🔒 **Confiabilidade**
- Offsets commitados
- Reprocessamento possível
- Durabilidade configurável

### 📈 **Escalabilidade**
- Partições múltiplas
- Consumidores em grupo
- Brokers adicionais

## Casos de Uso Implementados

### 🛒 **E-commerce Real-time**
- Eventos de usuário (login, view, purchase)
- Analytics em tempo real
- Detecção de fraude
- Sistema de recomendações

### 📊 **Data Pipeline**
- Captura de mudanças automática
- Transformação de dados
- Enriquecimento de eventos
- Distribuição para múltiplos consumidores

---

**Próximo**: [CDC SQL Server](./02-cdc-sqlserver.md)
