#Requires -Version 7

<#
.SYNOPSIS
    Configura LocalStack con SQS, SNS, y Lambda runtime
.DESCRIPTION
    Script completo para inicializar todos los recursos AWS en LocalStack
    VERSIÓN POWERSHELL 7 - JSON escapado correctamente
.NOTES
    Versión: 2.1 - PowerShell 7 Compatible
#>

$ErrorActionPreference = "Stop"

Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   SETUP COMPLETO DE LOCALSTACK - SQS, SNS, LAMBDA        ║" -ForegroundColor Cyan
Write-Host "║   Curso: Microservicios y Serverless con .NET            ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Cyan

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

Write-Host "`n🔍 Verificando que LocalStack esté ejecutándose..." -ForegroundColor Yellow

try {
    $healthCheck = Invoke-RestMethod -Uri "http://localhost:4566/_localstack/health" -Method Get
    Write-Host "✅ LocalStack está disponible" -ForegroundColor Green
    
    # Verificar servicios habilitados
    Write-Host "`n📋 Servicios habilitados:" -ForegroundColor Yellow
    $healthCheck.services | ConvertTo-Json
    
} catch {
    Write-Host "❌ LocalStack no está disponible. Ejecuta 'docker-compose up -d' primero." -ForegroundColor Red
    exit 1
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "Creando Colas SQS" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

Write-Host "Creando order-processing-queue..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sqs create-queue --queue-name order-processing-queue --attributes VisibilityTimeout=30,MessageRetentionPeriod=345600

Write-Host "Creando order-high-priority-queue..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sqs create-queue --queue-name order-high-priority-queue --attributes VisibilityTimeout=30,MessageRetentionPeriod=345600

Write-Host "Creando order-processing-dlq (Dead Letter Queue)..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sqs create-queue --queue-name order-processing-dlq --attributes MessageRetentionPeriod=1209600

Write-Host "Creando email-notifications-queue..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sqs create-queue --queue-name email-notifications-queue --attributes VisibilityTimeout=30,MessageRetentionPeriod=345600

Write-Host "Creando inventory-updates-queue..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sqs create-queue --queue-name inventory-updates-queue --attributes VisibilityTimeout=30,MessageRetentionPeriod=345600

Write-Host "✅ Colas SQS creadas exitosamente" -ForegroundColor Green

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "Creando Topicos SNS" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

Write-Host "Creando topico OrderCreated..." -ForegroundColor Yellow
$orderCreatedArn = docker exec localstack-aws awslocal sns create-topic --name OrderCreated --output text --query TopicArn
Write-Host "✅ Topico OrderCreated creado: $orderCreatedArn" -ForegroundColor Green

Write-Host "Creando topico OrderCompleted..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sns create-topic --name OrderCompleted --output text | Out-Null

Write-Host "Creando topico OrderCancelled..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sns create-topic --name OrderCancelled --output text | Out-Null

Write-Host "Creando topico StockReserved..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sns create-topic --name StockReserved --output text | Out-Null

Write-Host "✅ Topicos SNS creados exitosamente" -ForegroundColor Green

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "Configurando Suscripciones SNS a SQS" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

Write-Host "Obteniendo ARN de email-notifications-queue..." -ForegroundColor Yellow
$emailQueueArn = docker exec localstack-aws awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/email-notifications-queue --attribute-names QueueArn --output text --query Attributes.QueueArn

Write-Host "Obteniendo ARN de inventory-updates-queue..." -ForegroundColor Yellow
$inventoryQueueArn = docker exec localstack-aws awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/inventory-updates-queue --attribute-names QueueArn --output text --query Attributes.QueueArn

Write-Host "Suscribiendo email-notifications-queue a OrderCreated..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sns subscribe --topic-arn $orderCreatedArn --protocol sqs --notification-endpoint $emailQueueArn | Out-Null

Write-Host "Suscribiendo inventory-updates-queue a OrderCreated..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sns subscribe --topic-arn $orderCreatedArn --protocol sqs --notification-endpoint $inventoryQueueArn | Out-Null

Write-Host "✅ Suscripciones configuradas exitosamente" -ForegroundColor Green

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "Configurando Permisos de Colas" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# SOLUCIÓN POWERSHELL 7: Usar archivo temporal para JSON complejo
$queuePolicy = @{
    Policy = @{
        Statement = @(
            @{
                Effect = "Allow"
                Principal = "*"
                Action = "sqs:SendMessage"
                Resource = "*"
            }
        )
    } | ConvertTo-Json -Compress
} | ConvertTo-Json -Compress

# Crear archivo temporal
$tempPolicyFile = New-TemporaryFile
$queuePolicy | Out-File -FilePath $tempPolicyFile.FullName -Encoding utf8 -NoNewline

# Copiar al contenedor
docker cp $tempPolicyFile.FullName localstack-aws:/tmp/queue-policy.json

Write-Host "Configurando permisos para email-notifications-queue..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sqs set-queue-attributes --queue-url http://localhost:4566/000000000000/email-notifications-queue --attributes file:///tmp/queue-policy.json

Write-Host "Configurando permisos para inventory-updates-queue..." -ForegroundColor Yellow
docker exec localstack-aws awslocal sqs set-queue-attributes --queue-url http://localhost:4566/000000000000/inventory-updates-queue --attributes file:///tmp/queue-policy.json

# Limpiar archivo temporal
Remove-Item $tempPolicyFile.FullName -Force

Write-Host "✅ Permisos configurados exitosamente" -ForegroundColor Green

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "Configurando Dead Letter Queue" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

Write-Host "Obteniendo ARN de order-processing-dlq..." -ForegroundColor Yellow
$dlqArn = docker exec localstack-aws awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/order-processing-dlq --attribute-names QueueArn --output text --query Attributes.QueueArn

Write-Host "Configurando DLQ para order-processing-queue..." -ForegroundColor Yellow

# SOLUCIÓN POWERSHELL 7: Usar archivo temporal para RedrivePolicy
$redrivePolicy = @{
    RedrivePolicy = @{
        deadLetterTargetArn = $dlqArn
        maxReceiveCount = 3
    } | ConvertTo-Json -Compress
} | ConvertTo-Json -Compress

$tempDlqFile = New-TemporaryFile
$redrivePolicy | Out-File -FilePath $tempDlqFile.FullName -Encoding utf8 -NoNewline

docker cp $tempDlqFile.FullName localstack-aws:/tmp/dlq-policy.json
docker exec localstack-aws awslocal sqs set-queue-attributes --queue-url http://localhost:4566/000000000000/order-processing-queue --attributes file:///tmp/dlq-policy.json

Remove-Item $tempDlqFile.FullName -Force

Write-Host "✅ DLQ configurada exitosamente (max 3 intentos)" -ForegroundColor Green

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "Configurando Lambda Runtime" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

Write-Host "Creando rol IAM para Lambdas..." -ForegroundColor Yellow

# SOLUCIÓN POWERSHELL 7: Usar archivo temporal para AssumeRolePolicy
$assumeRolePolicy = @{
    Version = "2012-10-17"
    Statement = @(
        @{
            Effect = "Allow"
            Principal = @{
                Service = "lambda.amazonaws.com"
            }
            Action = "sts:AssumeRole"
        }
    )
} | ConvertTo-Json -Depth 10

$tempRoleFile = New-TemporaryFile
$assumeRolePolicy | Out-File -FilePath $tempRoleFile.FullName -Encoding utf8 -NoNewline

docker cp $tempRoleFile.FullName localstack-aws:/tmp/assume-role-policy.json

try {
    docker exec localstack-aws awslocal iam create-role --role-name lambda-execution-role --assume-role-policy-document file:///tmp/assume-role-policy.json | Out-Null
    Write-Host "✅ Rol IAM creado: lambda-execution-role" -ForegroundColor Green
} catch {
    Write-Host "⚠️  Rol IAM ya existe (puede ser normal)" -ForegroundColor Yellow
} finally {
    Remove-Item $tempRoleFile.FullName -Force
}

Write-Host "Adjuntando políticas de ejecución..." -ForegroundColor Yellow

# Adjuntar políticas
try {
    docker exec localstack-aws awslocal iam attach-role-policy --role-name lambda-execution-role --policy-arn arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole 2>&1 | Out-Null
    Write-Host "✅ Política AWSLambdaBasicExecutionRole adjuntada" -ForegroundColor Green
} catch {
    Write-Host "⚠️  Política ya adjuntada" -ForegroundColor Yellow
}

try {
    docker exec localstack-aws awslocal iam attach-role-policy --role-name lambda-execution-role --policy-arn arn:aws:iam::aws:policy/AmazonSQSFullAccess 2>&1 | Out-Null
    Write-Host "✅ Política AmazonSQSFullAccess adjuntada" -ForegroundColor Green
} catch {
    Write-Host "⚠️  Política ya adjuntada" -ForegroundColor Yellow
}

try {
    docker exec localstack-aws awslocal iam attach-role-policy --role-name lambda-execution-role --policy-arn arn:aws:iam::aws:policy/AmazonS3FullAccess 2>&1 | Out-Null
    Write-Host "✅ Política AmazonS3FullAccess adjuntada" -ForegroundColor Green
} catch {
    Write-Host "⚠️  Política ya adjuntada" -ForegroundColor Yellow
}

Write-Host "`n✅ Rol IAM configurado exitosamente para Lambdas" -ForegroundColor Green

Write-Host "`nℹ️  Las funciones Lambda se desplegarán usando el script:" -ForegroundColor Cyan
Write-Host "   Lambdas/Deploy-Lambdas.ps1" -ForegroundColor Yellow

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "VERIFICACION DE RECURSOS CREADOS" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

Write-Host "`n📦 Colas SQS:" -ForegroundColor Yellow
docker exec localstack-aws awslocal sqs list-queues

Write-Host "`n📢 Topicos SNS:" -ForegroundColor Yellow
docker exec localstack-aws awslocal sns list-topics

Write-Host "`n🔗 Suscripciones SNS:" -ForegroundColor Yellow
docker exec localstack-aws awslocal sns list-subscriptions --output table

Write-Host "`n👤 Roles IAM:" -ForegroundColor Yellow
docker exec localstack-aws awslocal iam list-roles --query 'Roles[?RoleName==`lambda-execution-role`].[RoleName, Arn]' --output table

Write-Host "`n📋 Políticas adjuntas al rol Lambda:" -ForegroundColor Yellow
docker exec localstack-aws awslocal iam list-attached-role-policies --role-name lambda-execution-role --output table

Write-Host "`n╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║  ✅ LocalStack configurado exitosamente!                  ║" -ForegroundColor Green
Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Green

Write-Host "`n📍 Endpoint LocalStack: http://localhost:4566" -ForegroundColor Cyan
Write-Host "📍 Región configurada: us-east-1" -ForegroundColor Cyan

Write-Host "`n🚀 PRÓXIMOS PASOS:" -ForegroundColor Yellow
Write-Host "   1. Implementar Lambdas (carpeta Lambdas/)" -ForegroundColor White
Write-Host "   2. Ejecutar: Lambdas/Deploy-Lambdas.ps1" -ForegroundColor White
Write-Host "   3. Probar con: dotnet run (demo SQS/SNS)" -ForegroundColor White
Write-Host "   4. Ver logs: awslocal logs tail /aws/lambda/<FunctionName>" -ForegroundColor White

Write-Host "`n=========================================" -ForegroundColor Green
Write-Host "Setup completado - Listo para desarrollo!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
