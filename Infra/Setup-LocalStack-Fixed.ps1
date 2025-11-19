# Script para inicializar LocalStack ejecutando comandos directamente
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Iniciando LocalStack" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# Reiniciar LocalStack
Write-Host "`nReiniciando LocalStack..." -ForegroundColor Yellow
docker-compose down -v
docker-compose up --build -d

# Esperar a que LocalStack este listo
Write-Host "Esperando a que LocalStack inicie (20 segundos)..." -ForegroundColor Yellow
Start-Sleep -Seconds 20

# Verificar que LocalStack esta corriendo
$container = docker ps --filter "name=localstack-aws" --format "{{.Names}}"
if ($container -ne "localstack-aws") {
    Write-Host "Error: LocalStack no esta corriendo" -ForegroundColor Red
    exit 1
}

Write-Host "LocalStack iniciado correctamente" -ForegroundColor Green

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "Creando Colas SQS" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# Crear colas SQS
Write-Host "Creando order-processing-queue..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sqs create-queue --queue-name order-processing-queue --attributes VisibilityTimeout=30,MessageRetentionPeriod=345600

Write-Host "Creando order-high-priority-queue..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sqs create-queue --queue-name order-high-priority-queue --attributes VisibilityTimeout=30,MessageRetentionPeriod=345600

Write-Host "Creando order-processing-dlq..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sqs create-queue --queue-name order-processing-dlq --attributes MessageRetentionPeriod=1209600

Write-Host "Creando email-notifications-queue..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sqs create-queue --queue-name email-notifications-queue --attributes VisibilityTimeout=30,MessageRetentionPeriod=345600

Write-Host "Creando inventory-updates-queue..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sqs create-queue --queue-name inventory-updates-queue --attributes VisibilityTimeout=30,MessageRetentionPeriod=345600

Write-Host "Colas SQS creadas exitosamente" -ForegroundColor Green

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "Creando Topicos SNS" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# Crear topicos SNS
Write-Host "Creando topico OrderCreated..." -ForegroundColor Yellow
$orderCreatedArn = docker exec localstack-aws awslocal sns create-topic --name OrderCreated --output text --query 'TopicArn'
Write-Host "Topico OrderCreated creado: $orderCreatedArn" -ForegroundColor Green

Write-Host "Creando topico OrderCompleted..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sns create-topic --name OrderCompleted --output text

Write-Host "Creando topico OrderCancelled..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sns create-topic --name OrderCancelled --output text

Write-Host "Creando topico StockReserved..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sns create-topic --name StockReserved --output text

Write-Host "Topicos SNS creados exitosamente" -ForegroundColor Green

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "Configurando Suscripciones SNS a SQS" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# Obtener ARNs de las colas
Write-Host "Obteniendo ARN de email-notifications-queue..." -ForegroundColor Yellow
$emailQueueArn = docker exec localstack-aws awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/email-notifications-queue --attribute-names QueueArn --output text --query 'Attributes.QueueArn'

Write-Host "Obteniendo ARN de inventory-updates-queue..." -ForegroundColor Yellow
$inventoryQueueArn = docker exec localstack-aws awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/inventory-updates-queue --attribute-names QueueArn --output text --query 'Attributes.QueueArn'

# Suscribir colas al topico OrderCreated
Write-Host "Suscribiendo email-notifications-queue a OrderCreated..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sns subscribe --topic-arn $orderCreatedArn --protocol sqs --notification-endpoint $emailQueueArn

Write-Host "Suscribiendo inventory-updates-queue a OrderCreated..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sns subscribe --topic-arn $orderCreatedArn --protocol sqs --notification-endpoint $inventoryQueueArn

Write-Host "Suscripciones configuradas exitosamente" -ForegroundColor Green

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "Configurando Permisos de Colas" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# Configurar permisos para que SNS pueda enviar a SQS
Write-Host "Configurando permisos para email-notifications-queue..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sqs set-queue-attributes --queue-url http://localhost:4566/000000000000/email-notifications-queue --attributes '{\"Policy\":\"{\\\"Statement\\\":[{\\\"Effect\\\":\\\"Allow\\\",\\\"Principal\\\":\\\"*\\\",\\\"Action\\\":\\\"sqs:SendMessage\\\",\\\"Resource\\\":\\\"*\\\"}]}\"}'

Write-Host "Configurando permisos para inventory-updates-queue..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sqs set-queue-attributes --queue-url http://localhost:4566/000000000000/inventory-updates-queue --attributes '{\"Policy\":\"{\\\"Statement\\\":[{\\\"Effect\\\":\\\"Allow\\\",\\\"Principal\\\":\\\"*\\\",\\\"Action\\\":\\\"sqs:SendMessage\\\",\\\"Resource\\\":\\\"*\\\"}]}\"}'

Write-Host "Permisos configurados exitosamente" -ForegroundColor Green

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "Configurando Dead Letter Queue" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# Configurar Dead Letter Queue
Write-Host "Obteniendo ARN de order-processing-dlq..." -ForegroundColor Yellow
$dlqArn = docker exec localstack-aws awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/order-processing-dlq --attribute-names QueueArn --output text --query 'Attributes.QueueArn'

Write-Host "Configurando DLQ para order-processing-queue..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sqs set-queue-attributes --queue-url http://localhost:4566/000000000000/order-processing-queue --attributes "{`"RedrivePolicy`":`"{\\`"deadLetterTargetArn\\`":\\`"$dlqArn\\`",\\`"maxReceiveCount\\`":3}`"}"

Write-Host "DLQ configurada exitosamente (max 3 intentos)" -ForegroundColor Green

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "VERIFICACION DE RECURSOS CREADOS" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

Write-Host "`nColas SQS:" -ForegroundColor Yellow
docker exec localstack-aws awslocal sqs list-queues

Write-Host "`nTopicos SNS:" -ForegroundColor Yellow
docker exec localstack-aws awslocal sns list-topics

Write-Host "`nSuscripciones SNS:" -ForegroundColor Yellow
docker exec localstack-aws awslocal sns list-subscriptions

Write-Host "`n=========================================" -ForegroundColor Green
Write-Host "LocalStack configurado y listo para usar!" -ForegroundColor Green
Write-Host "Endpoint: http://localhost:4566" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green