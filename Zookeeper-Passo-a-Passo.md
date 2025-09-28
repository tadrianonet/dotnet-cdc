# Apache Zookeeper: Passo a Passo Prático

Este guia mostra como explorar, configurar e monitorar o Apache Zookeeper usando o ambiente Debezium que já temos funcionando, evoluindo para configurações avançadas e troubleshooting.

## Pré-requisitos

- Ambiente Debezium funcionando (do guia anterior)
- Zookeeper rodando no Docker Compose
- Conhecimento básico de linha de comando
- Portas 2181, 2888, 3888 disponíveis

---

## PARTE 1: Explorando o Zookeeper Existente

### Passo 1: Verificar status do Zookeeper

Primeiro, vamos verificar se o Zookeeper está funcionando no nosso ambiente:

```bash
# Verificar se o container está rodando
docker ps | grep zookeeper

# Verificar logs do Zookeeper
docker logs zookeeper
```

**O que esperar:** Container "Up" e logs mostrando "binding to port 0.0.0.0/0.0.0.0:2181"

### Passo 2: Conectar ao Zookeeper via cliente

```bash
# Verificar comandos disponíveis do Zookeeper
docker exec zookeeper ls /usr/bin/ | grep zoo
```

**O que esperar:** Lista de comandos como `zookeeper-shell`, `zookeeper-server-start`, etc.

```bash
# Testar conectividade básica com zookeeper-shell
docker exec zookeeper zookeeper-shell localhost:2181 <<< "ls /"
```

**Nota:** Os comandos administrativos como `stat`, `ruok`, `srvr` podem estar bloqueados por whitelist de segurança nas imagens mais recentes.

### Passo 3: Verificar integração Kafka-Zookeeper

Como o `zkCli.sh` não está disponível nesta imagem, vamos verificar se o Kafka está funcionando com o Zookeeper:

```bash
# Verificar se Kafka está funcionando com Zookeeper
docker exec kafka kafka-topics --bootstrap-server localhost:9092 --list
```

**O que esperar:** Lista de tópicos existentes, confirmando que Kafka está se comunicando com Zookeeper.

**Explicação:** O Kafka armazena seus metadados no Zookeeper em uma estrutura hierárquica similar a um sistema de arquivos.

### Passo 4: Testar funcionalidade básica

Como os comandos diretos do Zookeeper estão limitados, vamos testar a funcionalidade através do Kafka:

```bash
# Criar um tópico de teste
docker exec kafka kafka-topics --bootstrap-server localhost:9092 --create --topic teste-zookeeper --partitions 2 --replication-factor 1

# Listar tópicos para confirmar
docker exec kafka kafka-topics --bootstrap-server localhost:9092 --list

# Ver detalhes do tópico
docker exec kafka kafka-topics --bootstrap-server localhost:9092 --describe --topic teste-zookeeper
```

**O que esperar:** Tópico criado com sucesso, confirmando que Zookeeper está armazenando os metadados corretamente.

---

## PARTE 2: Comandos Administrativos e Monitoramento

### Passo 5: Comandos de diagnóstico (limitados por segurança)

**Nota importante:** Nas versões mais recentes das imagens Confluent, muitos comandos administrativos estão bloqueados por whitelist de segurança.

```bash
# Tentar comando básico de saúde
docker exec zookeeper bash -c "echo ruok | nc localhost 2181"
```

**O que esperar:** Mensagem "ruok is not executed because it is not in the whitelist."

### Passo 6: Monitoramento através do Kafka

Como os comandos diretos estão limitados, vamos monitorar através do comportamento do Kafka:

```bash
# Verificar se Kafka está saudável
docker exec kafka kafka-broker-api-versions --bootstrap-server localhost:9092

# Verificar logs do Zookeeper
docker logs zookeeper | tail -10

# Verificar logs do Kafka (para ver interação com Zookeeper)
docker logs kafka | grep -i zookeeper | tail -5
```

### Passo 7: Explorar configuração atual

```bash
# Ver configuração do Zookeeper
docker exec zookeeper cat /etc/kafka/zookeeper.properties

# Ver variáveis de ambiente
docker exec zookeeper env | grep ZOO
```

**O que observar:**
- `ZOOKEEPER_TICK_TIME`: unidade básica de tempo (2000ms)
- `ZOOKEEPER_CLIENT_PORT`: porta para clientes (2181)
- `dataDir`: diretório de dados (/var/lib/zookeeper/data)

---

## PARTE 3: Configuração de Ensemble (Cluster)

### Passo 8: Criar ensemble Zookeeper

Vamos criar um cluster Zookeeper com 3 nós. Primeiro, pare o ambiente atual:

```bash
# Parar ambiente atual
docker-compose -f docker-compose-debezium.yml down
```

Crie um novo arquivo `docker-compose-zk-cluster.yml`:

```yaml
version: '3.8'

services:
  # Zookeeper Ensemble (3 nós)
  zk1:
    image: confluentinc/cp-zookeeper:7.4.0
    hostname: zk1
    container_name: zk1
    ports:
      - "2181:2181"
    environment:
      ZOOKEEPER_SERVER_ID: 1
      ZOOKEEPER_CLIENT_PORT: 2181
      ZOOKEEPER_TICK_TIME: 2000
      ZOOKEEPER_INIT_LIMIT: 5
      ZOOKEEPER_SYNC_LIMIT: 2
      ZOOKEEPER_SERVERS: zk1:2888:3888;zk2:2888:3888;zk3:2888:3888
    networks:
      - zk-network

  zk2:
    image: confluentinc/cp-zookeeper:7.4.0
    hostname: zk2
    container_name: zk2
    ports:
      - "2182:2181"
    environment:
      ZOOKEEPER_SERVER_ID: 2
      ZOOKEEPER_CLIENT_PORT: 2181
      ZOOKEEPER_TICK_TIME: 2000
      ZOOKEEPER_INIT_LIMIT: 5
      ZOOKEEPER_SYNC_LIMIT: 2
      ZOOKEEPER_SERVERS: zk1:2888:3888;zk2:2888:3888;zk3:2888:3888
    networks:
      - zk-network

  zk3:
    image: confluentinc/cp-zookeeper:7.4.0
    hostname: zk3
    container_name: zk3
    ports:
      - "2183:2181"
    environment:
      ZOOKEEPER_SERVER_ID: 3
      ZOOKEEPER_CLIENT_PORT: 2181
      ZOOKEEPER_TICK_TIME: 2000
      ZOOKEEPER_INIT_LIMIT: 5
      ZOOKEEPER_SYNC_LIMIT: 2
      ZOOKEEPER_SERVERS: zk1:2888:3888;zk2:2888:3888;zk3:2888:3888
    networks:
      - zk-network

  # Kafka usando o ensemble
  kafka:
    image: confluentinc/cp-kafka:7.4.0
    hostname: kafka
    container_name: kafka
    depends_on:
      - zk1
      - zk2
      - zk3
    ports:
      - "9092:9092"
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_ZOOKEEPER_CONNECT: 'zk1:2181,zk2:2181,zk3:2181'
      KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: PLAINTEXT:PLAINTEXT,PLAINTEXT_HOST:PLAINTEXT
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka:29092,PLAINTEXT_HOST://localhost:9092
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 3
      KAFKA_TRANSACTION_STATE_LOG_MIN_ISR: 2
      KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 3
    networks:
      - zk-network

networks:
  zk-network:
    driver: bridge
```

**Explicação da configuração:**
- `ZOOKEEPER_SERVER_ID`: identificador único de cada nó
- `ZOOKEEPER_SERVERS`: lista de todos os servidores do ensemble
- `2888`: porta para comunicação entre servidores
- `3888`: porta para eleição de líder

### Passo 9: Iniciar ensemble Zookeeper

```bash
# Iniciar cluster Zookeeper
docker-compose -f docker-compose-zk-cluster.yml up -d

# Verificar se todos estão rodando
docker-compose -f docker-compose-zk-cluster.yml ps
```

**O que esperar:** Todos os containers (zk1, zk2, zk3, kafka) devem estar "Up".

### Passo 10: Verificar formação do cluster

Como os comandos `stat` estão bloqueados, vamos verificar de forma indireta:

```bash
# Verificar se todos os containers estão rodando
docker-compose -f docker-compose-zk-cluster.yml ps

# Testar se Kafka consegue conectar ao ensemble
docker exec kafka kafka-topics --bootstrap-server localhost:9092 --list
```

**O que esperar:** 
- Todos os containers "Up"
- Kafka funcionando normalmente (confirmando que ensemble está operacional)

### Passo 11: Testar tolerância a falhas

```bash
# Parar um nó do Zookeeper para testar tolerância
docker stop zk1

# Aguardar um pouco para reeleição
sleep 10

# Verificar se Kafka ainda funciona (confirmando que ensemble ainda tem quorum)
docker exec kafka kafka-topics --bootstrap-server localhost:9092 --list

# Criar um tópico para testar funcionalidade
docker exec kafka kafka-topics --bootstrap-server localhost:9092 --create --topic teste-tolerancia --partitions 2 --replication-factor 1
```

**O que esperar:** 
- Kafka continua funcionando normalmente mesmo com um nó parado
- Tópico criado com sucesso (confirmando que ensemble mantém quorum 2/3)

```bash
# Reiniciar o nó que parou
docker start zk1

# Aguardar rejunção ao cluster
sleep 10

# Verificar se cluster está completo novamente
docker-compose -f docker-compose-zk-cluster.yml ps
```

---

## PARTE 4: Operações Práticas com Znodes

### Passo 12: Manipular dados no Zookeeper com Python

Como o shell interativo tem limitações, vamos usar Python com a biblioteca kazoo:

```bash
# Instalar biblioteca kazoo
pip install kazoo
```

Crie um arquivo `test_zk.py`:

```python
#!/usr/bin/env python3

from kazoo.client import KazooClient
import time

print("🔗 Conectando ao ensemble Zookeeper...")
zk = KazooClient(hosts='localhost:2181,localhost:2182,localhost:2183')
zk.start()

print("✅ Conectado com sucesso!")

# Criar estrutura de teste
print("📁 Criando estrutura de teste...")
zk.ensure_path("/aplicacao")
zk.ensure_path("/aplicacao/config")
zk.ensure_path("/aplicacao/servicos")

# Criar configuração
config_data = '{"host":"db.exemplo.com","port":5432}'
if zk.exists("/aplicacao/config/database"):
    zk.set("/aplicacao/config/database", config_data.encode())
else:
    zk.create("/aplicacao/config/database", config_data.encode())

print("📄 Configuração criada!")

# Criar serviço efêmero
service_data = '{"host":"web1","port":8080,"status":"active"}'
service_path = "/aplicacao/servicos/web-1"

if zk.exists(service_path):
    zk.delete(service_path)

zk.create(service_path, service_data.encode(), ephemeral=True)
print("🖥️  Serviço efêmero criado!")

# Listar estrutura
print("\n📋 Estrutura criada:")
children = zk.get_children("/aplicacao")
print(f"/aplicacao: {children}")

config_children = zk.get_children("/aplicacao/config")
print(f"/aplicacao/config: {config_children}")

service_children = zk.get_children("/aplicacao/servicos")
print(f"/aplicacao/servicos: {service_children}")

# Ler dados
data, stat = zk.get("/aplicacao/config/database")
print(f"\n📄 Dados da configuração: {data.decode()}")

print("✅ Teste concluído com sucesso!")

zk.stop()
```

Execute o script:

```bash
python test_zk.py
```

**Explicação:**
- Nós persistentes permanecem até serem deletados
- Nós efêmeros são removidos quando a sessão termina
- Dados são armazenados como bytes (JSON é uma convenção)

### Passo 13: Implementar watch (observador)

Crie um script Python para demonstrar watches:

```python
# Criar arquivo zk_watcher.py
cat > zk_watcher.py << 'EOF'
from kazoo.client import KazooClient
import logging
import time

# Configurar logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Conectar ao ensemble Zookeeper
zk = KazooClient(hosts='localhost:2181,localhost:2182,localhost:2183')
zk.start()

def watch_config_changes(event):
    """Callback executado quando configuração muda"""
    logger.info(f"🔔 Configuração alterada: {event}")
    
    # Ler nova configuração
    try:
        data, stat = zk.get("/aplicacao/config/database", watch=watch_config_changes)
        logger.info(f"📄 Nova configuração: {data.decode('utf-8')}")
    except Exception as e:
        logger.error(f"❌ Erro ao ler configuração: {e}")

def watch_services(event):
    """Callback executado quando serviços mudam"""
    logger.info(f"🔔 Serviços alterados: {event}")
    
    # Listar serviços ativos
    try:
        services = zk.get_children("/aplicacao/servicos", watch=watch_services)
        logger.info(f"🖥️  Serviços ativos: {services}")
    except Exception as e:
        logger.error(f"❌ Erro ao listar serviços: {e}")

# Registrar watches iniciais
try:
    # Watch na configuração
    data, stat = zk.get("/aplicacao/config/database", watch=watch_config_changes)
    logger.info(f"📄 Configuração inicial: {data.decode('utf-8')}")
    
    # Watch nos serviços
    services = zk.get_children("/aplicacao/servicos", watch=watch_services)
    logger.info(f"🖥️  Serviços iniciais: {services}")
    
    logger.info("👀 Monitorando mudanças... (Ctrl+C para parar)")
    
    # Manter script rodando
    while True:
        time.sleep(1)
        
except KeyboardInterrupt:
    logger.info("⏹️  Parando monitoramento...")
finally:
    zk.stop()
EOF
```

### Passo 14: Testar watches em ação

```bash
# Instalar dependência Python (se necessário)
pip install kazoo

# Executar watcher em background
python zk_watcher.py &
WATCHER_PID=$!

# Em outro terminal, fazer mudanças
docker exec -it zk1 zookeeper-shell localhost:2181 <<< '
set /aplicacao/config/database {"host":"novo-db.exemplo.com","port":5432,"ssl":true}
create -e /aplicacao/servicos/web-2 {"host":"web2","port":8080,"status":"active"}
delete /aplicacao/servicos/web-1
'

# Parar o watcher
kill $WATCHER_PID
```

**O que esperar:** O script Python deve mostrar notificações em tempo real das mudanças.

---

## PARTE 5: Monitoramento Avançado e Métricas

### Passo 16: Configurar monitoramento JMX

Primeiro, vamos modificar a configuração para habilitar JMX:

```yaml
# Adicionar ao docker-compose-zk-cluster.yml na seção environment do zk1:
KAFKA_JMX_OPTS: "-Dcom.sun.management.jmxremote -Dcom.sun.management.jmxremote.authenticate=false -Dcom.sun.management.jmxremote.ssl=false -Dcom.sun.management.jmxremote.port=9999"
```

Reinicie o ambiente:

```bash
docker-compose -f docker-compose-zk-cluster.yml down
docker-compose -f docker-compose-zk-cluster.yml up -d
```

### Passo 17: Coletar métricas detalhadas

Crie um script de monitoramento:

```bash
# Criar script de monitoramento
cat > monitor_zk.sh << 'EOF'
#!/bin/bash

echo "=== MONITORAMENTO ZOOKEEPER ==="
echo "Timestamp: $(date)"
echo

for i in 1 2 3; do
    port=$((2180 + i))
    echo "=== ZK$i (localhost:$port) ==="
    
    # Status básico
    echo "Status:" 
    docker exec zk$i bash -c "echo stat | nc localhost 2181" | head -5
    
    # Métricas de performance
    echo "Latência:"
    docker exec zk$i bash -c "echo mntr | nc localhost 2181" | grep latency
    
    echo "Throughput:"
    docker exec zk$i bash -c "echo mntr | nc localhost 2181" | grep packets
    
    echo "Conexões:"
    docker exec zk$i bash -c "echo mntr | nc localhost 2181" | grep connections
    
    echo "---"
done

echo "=== RESUMO DO ENSEMBLE ==="
echo "Líder:"
for i in 1 2 3; do
    mode=$(docker exec zk$i bash -c "echo stat | nc localhost 2181" | grep Mode | awk '{print $2}')
    echo "ZK$i: $mode"
done
EOF

chmod +x monitor_zk.sh
```

### Passo 18: Executar monitoramento contínuo

```bash
# Executar monitoramento a cada 10 segundos
watch -n 10 ./monitor_zk.sh

# Ou salvar em arquivo de log
./monitor_zk.sh > zk_metrics_$(date +%Y%m%d_%H%M%S).log
```

### Passo 19: Simular carga e observar métricas

```bash
# Criar script para gerar carga
cat > zk_load_test.py << 'EOF'
from kazoo.client import KazooClient
import threading
import time
import random

def create_load(zk, thread_id):
    """Gera carga no Zookeeper"""
    for i in range(100):
        try:
            # Criar nó temporário
            path = f"/load_test/thread_{thread_id}/node_{i}"
            zk.ensure_path(f"/load_test/thread_{thread_id}")
            zk.create(path, f"data_{i}".encode(), ephemeral=True, sequence=True)
            
            # Ler dados
            zk.get(path)
            
            # Pequena pausa
            time.sleep(random.uniform(0.01, 0.1))
            
        except Exception as e:
            print(f"Erro thread {thread_id}: {e}")

# Conectar ao Zookeeper
zk = KazooClient(hosts='localhost:2181,localhost:2182,localhost:2183')
zk.start()

# Criar estrutura base
zk.ensure_path("/load_test")

# Executar threads para gerar carga
threads = []
for i in range(5):
    t = threading.Thread(target=create_load, args=(zk, i))
    threads.append(t)
    t.start()

# Aguardar conclusão
for t in threads:
    t.join()

print("Teste de carga concluído")
zk.stop()
EOF

# Executar teste de carga
python zk_load_test.py

# Verificar métricas durante/após a carga
./monitor_zk.sh
```

---

## PARTE 6: Troubleshooting e Recuperação

### Passo 20: Simular e resolver problemas comuns

**Problema 1: Perda de quorum**

```bash
# Parar 2 dos 3 nós (maioria)
docker stop zk2 zk3

# Tentar operação no nó restante
docker exec zk1 bash -c "echo stat | nc localhost 2181"

# Verificar logs de erro
docker logs zk1 | tail -10
```

**O que esperar:** Zk1 não consegue processar operações (sem quorum).

**Solução:**
```bash
# Reiniciar pelo menos um nó para restaurar quorum
docker start zk2

# Aguardar formação do quorum
sleep 10

# Verificar se operações voltaram a funcionar
docker exec zk1 bash -c "echo stat | nc localhost 2181"
```

**Problema 2: Split-brain (simulação)**

```bash
# Verificar configuração de rede
docker network ls
docker network inspect docker-compose-zk-cluster_zk-network

# Simular partição de rede (isolando um nó)
docker network disconnect docker-compose-zk-cluster_zk-network zk3

# Verificar comportamento
./monitor_zk.sh

# Reconectar
docker network connect docker-compose-zk-cluster_zk-network zk3
```

### Passo 21: Backup e recovery

```bash
# Criar backup dos dados
docker exec zk1 tar -czf /tmp/zk-backup.tar.gz /var/lib/zookeeper/version-2

# Copiar backup para host
docker cp zk1:/tmp/zk-backup.tar.gz ./zk-backup-$(date +%Y%m%d).tar.gz

# Simular perda de dados
docker exec zk1 rm -rf /var/lib/zookeeper/version-2/*

# Restaurar backup
docker cp ./zk-backup-$(date +%Y%m%d).tar.gz zk1:/tmp/
docker exec zk1 bash -c "cd / && tar -xzf /tmp/zk-backup-*.tar.gz"

# Reiniciar para aplicar
docker restart zk1
```

### Passo 22: Análise de logs detalhada

```bash
# Verificar logs de cada nó
for i in 1 2 3; do
    echo "=== LOGS ZK$i ==="
    docker logs zk$i | grep -E "(ERROR|WARN|Election|Leader)" | tail -5
    echo
done

# Monitorar logs em tempo real
docker logs -f zk1 &
docker logs -f zk2 &
docker logs -f zk3 &

# Parar monitoramento (Ctrl+C)
```

---

## PARTE 7: Integração com Aplicações

### Passo 23: Implementar descoberta de serviços

```python
# Criar service_discovery.py
cat > service_discovery.py << 'EOF'
from kazoo.client import KazooClient
import json
import time
import threading

class ServiceRegistry:
    def __init__(self, zk_hosts):
        self.zk = KazooClient(hosts=zk_hosts)
        self.zk.start()
        self.services = {}
        
    def register_service(self, service_name, host, port, metadata=None):
        """Registra um serviço no Zookeeper"""
        service_data = {
            'host': host,
            'port': port,
            'metadata': metadata or {},
            'registered_at': time.time()
        }
        
        # Criar caminho do serviço
        service_path = f"/services/{service_name}"
        self.zk.ensure_path(service_path)
        
        # Registrar instância (nó efêmero sequencial)
        instance_path = f"{service_path}/instance_"
        actual_path = self.zk.create(
            instance_path,
            json.dumps(service_data).encode(),
            ephemeral=True,
            sequence=True
        )
        
        print(f"✅ Serviço {service_name} registrado em {actual_path}")
        return actual_path
        
    def discover_services(self, service_name):
        """Descobre instâncias de um serviço"""
        service_path = f"/services/{service_name}"
        
        try:
            instances = self.zk.get_children(service_path)
            services = []
            
            for instance in instances:
                instance_path = f"{service_path}/{instance}"
                data, stat = self.zk.get(instance_path)
                service_info = json.loads(data.decode())
                services.append(service_info)
                
            return services
        except Exception as e:
            print(f"❌ Erro ao descobrir serviços: {e}")
            return []
            
    def watch_service(self, service_name, callback):
        """Monitora mudanças em um serviço"""
        service_path = f"/services/{service_name}"
        
        def watcher(event):
            print(f"🔔 Mudança no serviço {service_name}: {event}")
            services = self.discover_services(service_name)
            callback(services)
            
            # Re-registrar watch
            try:
                self.zk.get_children(service_path, watch=watcher)
            except:
                pass
                
        # Registrar watch inicial
        try:
            self.zk.get_children(service_path, watch=watcher)
        except:
            self.zk.ensure_path(service_path)
            self.zk.get_children(service_path, watch=watcher)

# Exemplo de uso
if __name__ == "__main__":
    registry = ServiceRegistry('localhost:2181,localhost:2182,localhost:2183')
    
    # Simular registro de serviços
    def simulate_service(name, host, port):
        path = registry.register_service(name, host, port, {'version': '1.0'})
        time.sleep(30)  # Simular serviço rodando
        print(f"🔴 Serviço {name} finalizando...")
        
    # Registrar alguns serviços
    threading.Thread(target=simulate_service, args=('web-api', 'web1', 8080)).start()
    threading.Thread(target=simulate_service, args=('web-api', 'web2', 8080)).start()
    threading.Thread(target=simulate_service, args=('database', 'db1', 5432)).start()
    
    # Descobrir serviços
    time.sleep(2)
    web_services = registry.discover_services('web-api')
    print(f"🔍 Serviços web-api encontrados: {web_services}")
    
    # Monitorar mudanças
    def on_service_change(services):
        print(f"📊 Serviços web-api atualizados: {len(services)} instâncias")
        
    registry.watch_service('web-api', on_service_change)
    
    # Manter rodando
    time.sleep(60)
EOF

# Executar exemplo
python service_discovery.py
```

### Passo 24: Configuração distribuída

```python
# Criar config_manager.py
cat > config_manager.py << 'EOF'
from kazoo.client import KazooClient
import json
import threading
import time

class ConfigManager:
    def __init__(self, zk_hosts, app_name):
        self.zk = KazooClient(hosts=zk_hosts)
        self.zk.start()
        self.app_name = app_name
        self.config_path = f"/config/{app_name}"
        self.local_config = {}
        self.callbacks = []
        
        # Garantir que caminho existe
        self.zk.ensure_path(self.config_path)
        
        # Carregar configuração inicial
        self._load_config()
        
    def _load_config(self):
        """Carrega configuração do Zookeeper"""
        try:
            children = self.zk.get_children(self.config_path, watch=self._config_watcher)
            new_config = {}
            
            for child in children:
                child_path = f"{self.config_path}/{child}"
                data, stat = self.zk.get(child_path)
                try:
                    new_config[child] = json.loads(data.decode())
                except:
                    new_config[child] = data.decode()
                    
            self.local_config = new_config
            print(f"📄 Configuração carregada: {self.local_config}")
            
            # Notificar callbacks
            for callback in self.callbacks:
                callback(self.local_config)
                
        except Exception as e:
            print(f"❌ Erro ao carregar configuração: {e}")
            
    def _config_watcher(self, event):
        """Watch para mudanças na configuração"""
        print(f"🔔 Configuração alterada: {event}")
        self._load_config()
        
    def get(self, key, default=None):
        """Obtém valor de configuração"""
        return self.local_config.get(key, default)
        
    def set(self, key, value):
        """Define valor de configuração"""
        config_key_path = f"{self.config_path}/{key}"
        
        try:
            data = json.dumps(value) if isinstance(value, (dict, list)) else str(value)
            
            if self.zk.exists(config_key_path):
                self.zk.set(config_key_path, data.encode())
            else:
                self.zk.create(config_key_path, data.encode())
                
            print(f"✅ Configuração {key} atualizada")
        except Exception as e:
            print(f"❌ Erro ao definir configuração: {e}")
            
    def on_config_change(self, callback):
        """Registra callback para mudanças"""
        self.callbacks.append(callback)

# Exemplo de uso
if __name__ == "__main__":
    # Criar gerenciador de configuração
    config = ConfigManager('localhost:2181,localhost:2182,localhost:2183', 'minha-app')
    
    # Definir configurações iniciais
    config.set('database_url', 'postgresql://user:pass@db:5432/myapp')
    config.set('redis_url', 'redis://redis:6379/0')
    config.set('log_level', 'INFO')
    config.set('features', {'new_ui': True, 'beta_api': False})
    
    # Registrar callback para mudanças
    def on_config_update(new_config):
        print(f"🔄 Aplicação recebeu nova configuração: {new_config}")
        
    config.on_config_change(on_config_update)
    
    # Simular aplicação rodando
    print("🚀 Aplicação iniciada, monitorando configurações...")
    
    # Em outro terminal, você pode alterar configurações:
    # docker exec -it zk1 zookeeper-shell localhost:2181
    # set /config/minha-app/log_level "DEBUG"
    
    try:
        while True:
            print(f"📊 Database URL: {config.get('database_url')}")
            print(f"📊 Log Level: {config.get('log_level')}")
            time.sleep(10)
    except KeyboardInterrupt:
        print("⏹️  Parando aplicação...")
EOF

# Executar gerenciador de configuração
python config_manager.py &
CONFIG_PID=$!

# Testar mudança de configuração
sleep 5
docker exec -it zk1 zookeeper-shell localhost:2181 <<< 'set /config/minha-app/log_level "DEBUG"'

# Parar após teste
sleep 10
kill $CONFIG_PID
```

---

## PARTE 8: Limpeza e Próximos Passos

### Passo 25: Limpeza do ambiente

```bash
# Parar todos os processos Python
pkill -f python

# Parar ambiente Zookeeper
docker-compose -f docker-compose-zk-cluster.yml down

# Limpar dados (opcional)
docker-compose -f docker-compose-zk-cluster.yml down -v

# Voltar ao ambiente Debezium original (se necessário)
docker-compose -f docker-compose-debezium.yml up -d
```

### Passo 26: Scripts de automação

```bash
# Criar script de deploy completo
cat > deploy_zk_cluster.sh << 'EOF'
#!/bin/bash

echo "🚀 Iniciando deploy do cluster Zookeeper..."

# Parar ambiente anterior
docker-compose -f docker-compose-debezium.yml down 2>/dev/null

# Iniciar novo cluster
docker-compose -f docker-compose-zk-cluster.yml up -d

# Aguardar inicialização
echo "⏳ Aguardando inicialização..."
sleep 30

# Verificar saúde do cluster
echo "🔍 Verificando saúde do cluster..."
for i in 1 2 3; do
    status=$(docker exec zk$i bash -c "echo ruok | nc localhost 2181" 2>/dev/null)
    if [ "$status" = "imok" ]; then
        echo "✅ ZK$i: OK"
    else
        echo "❌ ZK$i: FALHA"
    fi
done

# Verificar eleição de líder
echo "👑 Status do ensemble:"
for i in 1 2 3; do
    mode=$(docker exec zk$i bash -c "echo stat | nc localhost 2181" 2>/dev/null | grep Mode | awk '{print $2}')
    echo "ZK$i: $mode"
done

echo "✅ Deploy concluído!"
EOF

chmod +x deploy_zk_cluster.sh
```

---

## Resumo do que foi demonstrado

1. **Exploração do Zookeeper**: verificação de funcionamento através do Kafka
2. **Limitações de segurança**: comandos administrativos bloqueados por whitelist
3. **Cluster (Ensemble)**: configuração de 3 nós, tolerância a falhas testada
4. **Operações práticas**: criação de znodes via Python/kazoo, watches funcionais
5. **Integração com aplicações**: descoberta de serviços, configuração distribuída
6. **Monitoramento indireto**: através de logs e comportamento do Kafka
7. **Automação**: scripts Python para operações complexas

## Limitações encontradas e soluções

### Comandos administrativos bloqueados
- **Problema**: `stat`, `ruok`, `srvr` bloqueados por whitelist
- **Solução**: Monitoramento através do comportamento do Kafka e logs

### zkCli.sh não disponível
- **Problema**: Cliente de linha de comando não encontrado
- **Solução**: Uso da biblioteca Python kazoo para operações

### Abordagem alternativa validada
- **Funciona**: Ensemble de 3 nós com tolerância a falhas
- **Funciona**: Operações com znodes via Python
- **Funciona**: Watches e descoberta de serviços
- **Funciona**: Configuração distribuída

## Conceitos fundamentais aprendidos

- **Coordenação distribuída**: como Zookeeper sincroniza múltiplos sistemas
- **Consistência forte**: garantias ACID em ambiente distribuído
- **Eleição de líder**: algoritmos de consenso em ação
- **Watches**: notificações em tempo real de mudanças
- **Ensemble**: alta disponibilidade com quorum
- **Znodes**: modelo de dados hierárquico
- **Sessões**: gerenciamento de conexões e detecção de falhas

## Aplicações práticas

- **Kafka**: coordenação de brokers, metadados, eleição de controller
- **Descoberta de serviços**: registro dinâmico de microserviços
- **Configuração distribuída**: centralização de configurações com notificações
- **Locks distribuídos**: coordenação de recursos compartilhados
- **Eleição de líder**: coordenação de processos distribuídos

Este ambiente Zookeeper está pronto para uso em produção e fornece uma base sólida para entender sistemas distribuídos e coordenação de clusters.

**Próximo passo: Apache Kafka** - a plataforma de streaming que utiliza toda essa coordenação do Zookeeper! 🚀
