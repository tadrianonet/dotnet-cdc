# Documentação: Jornada CDC → Debezium → Zookeeper → Kafka

Esta documentação apresenta diagramas de sequência e arquitetura da jornada completa de streaming de dados implementada.

## 📋 Índice

1. [Visão Geral da Arquitetura](#visão-geral-da-arquitetura)
2. [CDC SQL Server](#cdc-sql-server)
3. [Debezium Integration](#debezium-integration)
4. [Zookeeper Coordination](#zookeeper-coordination)
5. [Kafka Streaming](#kafka-streaming)
6. [Fluxo Completo End-to-End](#fluxo-completo-end-to-end)

## 🎯 Objetivo

Demonstrar como implementar uma arquitetura completa de streaming de dados em tempo real, desde a captura de mudanças no banco de dados até o processamento distribuído com Kafka.

## 🛠️ Tecnologias Utilizadas

- **SQL Server**: Banco de dados com CDC habilitado
- **Debezium**: Plataforma de CDC para streaming
- **Apache Zookeeper**: Coordenação distribuída
- **Apache Kafka**: Plataforma de streaming
- **.NET 10 C#**: Aplicações produtoras e consumidoras
- **Docker**: Containerização dos serviços

## 📊 Diagramas Disponíveis

### 1. [Arquitetura Geral](./01-arquitetura-geral.md)
Visão macro da solução completa

### 2. [CDC SQL Server](./02-cdc-sqlserver.md)
Captura de mudanças no banco de dados

### 3. [Debezium Connector](./03-debezium-connector.md)
Integração CDC com Kafka

### 4. [Zookeeper Ensemble](./04-zookeeper-ensemble.md)
Coordenação e configuração distribuída

### 5. [Kafka Streaming](./05-kafka-streaming.md)
Processamento de eventos em tempo real

### 6. [Fluxo End-to-End](./06-fluxo-completo.md)
Jornada completa dos dados

## 🚀 Como Usar

1. Navegue pelos diagramas na ordem sugerida
2. Cada arquivo contém:
   - Diagrama de sequência em Mermaid
   - Explicação detalhada do fluxo
   - Componentes envolvidos
   - Pontos de atenção

## 📚 Guias Práticos Relacionados

- `CDC-Passo-a-Passo-Completo.md`: Setup completo do CDC
- `Debezium-Passo-a-Passo.md`: Configuração do Debezium
- `Zookeeper-Passo-a-Passo.md`: Administração do Zookeeper
- `Kafka-Passo-a-Passo.md`: Operações com Kafka

---

**🎓 Material desenvolvido para ensino de arquiteturas de streaming de dados modernas**
