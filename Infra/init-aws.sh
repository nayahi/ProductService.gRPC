#!/bin/bash

echo "========================================="
echo "Inicializando recursos AWS en LocalStack"
echo "========================================="

export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
export AWS_DEFAULT_REGION=us-east-1

echo "Esperando a que LocalStack este listo..."
sleep 5

echo ""
echo "Creando colas SQS..."

awslocal sqs create-queue --queue-name order-processing-queue --attributes VisibilityTimeout=30,MessageRetentionPeriod=345600
awslocal sqs create-queue --queue-name order-high-priority-queue --attributes VisibilityTimeout=30,MessageRetentionPeriod=345600
awslocal sqs create-queue --queue-name order-processing-dlq --attributes MessageRetentionPeriod=1209600
awslocal sqs create-queue --queue-name email-notifications-queue --attributes VisibilityTimeout=30,MessageRetentionPeriod=345600
awslocal sqs create-queue --queue-name inventory-updates-queue --attributes VisibilityTimeout=30,MessageRetentionPeriod=345600

echo "Colas SQS creadas exitosamente"

echo ""
echo "Creando topicos SNS..."

ORDER_CREATED_ARN=$(awslocal sns create-topic --name OrderCreated --output text --query 'TopicArn')
echo "Topico OrderCreated creado: $ORDER_CREATED_ARN"

ORDER_COMPLETED_ARN=$(awslocal sns create-topic --name OrderCompleted --output text --query 'TopicArn')
echo "Topico OrderCompleted creado: $ORDER_COMPLETED_ARN"

ORDER_CANCELLED_ARN=$(awslocal sns create-topic --name OrderCancelled --output text --query 'TopicArn')
echo "Topico OrderCancelled creado: $ORDER_CANCELLED_ARN"

STOCK_RESERVED_ARN=$(awslocal sns create-topic --name StockReserved --output text --query 'TopicArn')
echo "Topico StockReserved creado: $STOCK_RESERVED_ARN"

echo ""
echo "Configurando suscripciones SNS a SQS..."

EMAIL_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/email-notifications-queue --attribute-names QueueArn --output text --query 'Attributes.QueueArn')

INVENTORY_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/inventory-updates-queue --attribute-names QueueArn --output text --query 'Attributes.QueueArn')

awslocal sns subscribe --topic-arn $ORDER_CREATED_ARN --protocol sqs --notification-endpoint $EMAIL_QUEUE_ARN
echo "Cola email-notifications-queue suscrita a OrderCreated"

awslocal sns subscribe --topic-arn $ORDER_CREATED_ARN --protocol sqs --notification-endpoint $INVENTORY_QUEUE_ARN
echo "Cola inventory-updates-queue suscrita a OrderCreated"

awslocal sqs set-queue-attributes --queue-url http://localhost:4566/000000000000/email-notifications-queue --attributes '{"Policy":"{\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":\"*\",\"Action\":\"sqs:SendMessage\",\"Resource\":\"*\"}]}"}'

awslocal sqs set-queue-attributes --queue-url http://localhost:4566/000000000000/inventory-updates-queue --attributes '{"Policy":"{\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":\"*\",\"Action\":\"sqs:SendMessage\",\"Resource\":\"*\"}]}"}'

echo ""
echo "Configurando Dead Letter Queue..."

DLQ_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/order-processing-dlq --attribute-names QueueArn --output text --query 'Attributes.QueueArn')

awslocal sqs set-queue-attributes --queue-url http://localhost:4566/000000000000/order-processing-queue --attributes "{\"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"}"

echo "DLQ configurada para order-processing-queue (max 3 intentos)"

echo ""
echo "========================================="
echo "RECURSOS AWS CREADOS EN LOCALSTACK"
echo "========================================="
echo ""
echo "LocalStack listo para usar!"
echo "Endpoint: http://localhost:4566"
echo "========================================="
