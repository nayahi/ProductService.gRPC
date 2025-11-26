# TESTING DE RESERVAS DE STOCK - ProductService

Guía de testing paso a paso para probar los métodos de reserva de stock ANTES de implementar la saga completa.

---

## 📋 PREREQUISITOS

1. ✅ ProductService corriendo en puerto 7001
2. ✅ Datos de prueba inicializados (DbInitializer con reservas)
3. ✅ grpcurl instalado

---

## 🔍 PASO 0: Verificar Estado Inicial

### Ver productos disponibles

```bash
grpcurl -plaintext -d '{}' localhost:7001 productservice.ProductService/GetAllProducts
```

### Ver reservas en SQL Server

```sql
USE ECommerceGRPC;

-- Ver todas las reservas
SELECT 
    ReservationId,
    ProductId,
    OrderId,
    QuantityReserved,
    Status,
    CreatedAt,
    ConfirmedAt,
    ReleasedAt,
    ReleaseReason
FROM StockReservations
ORDER BY CreatedAt DESC;

-- Ver stock disponible por producto
SELECT 
    p.Id,
    p.Name,
    p.Stock as TotalStock,
    COALESCE(SUM(CASE WHEN sr.Status = 'Reserved' THEN sr.QuantityReserved ELSE 0 END), 0) as ReservedStock,
    p.Stock - COALESCE(SUM(CASE WHEN sr.Status = 'Reserved' THEN sr.QuantityReserved ELSE 0 END), 0) as AvailableStock
FROM Products p
LEFT JOIN StockReservations sr ON p.Id = sr.ProductId
GROUP BY p.Id, p.Name, p.Stock
ORDER BY p.Id;
```

**Resultado esperado:**
- Producto 1 (Laptop): Stock=15, Reservado=2, Disponible=13
- Producto 6 (Cafetera): Stock=30, Reservado=1, Disponible=29
- Producto 12 (LEGO): Stock=5, Reservado=1, Disponible=4

---

## 🧪 PASO 1: Test CheckAvailability (Consulta sin modificar)

### Test 1.1: Verificar disponibilidad con stock suficiente

```bash
grpcurl -plaintext -d '{
  "product_id": 1,
  "quantity": 5
}' localhost:7001 productservice.ProductService/CheckAvailability
```

**Resultado esperado:**
```json
{
  "productId": 1,
  "productName": "Laptop Dell XPS 15",
  "totalStock": 15,
  "reservedStock": 2,
  "availableStock": 13,
  "requestedQuantity": 5,
  "available": true,
  "message": "Stock disponible: 13 unidades"
}
```

### Test 1.2: Verificar disponibilidad con stock insuficiente

```bash
grpcurl -plaintext -d '{
  "product_id": 12,
  "quantity": 10
}' localhost:7001 productservice.ProductService/CheckAvailability
```

**Resultado esperado:**
```json
{
  "productId": 12,
  "productName": "LEGO Millennium Falcon",
  "totalStock": 5,
  "reservedStock": 1,
  "availableStock": 4,
  "requestedQuantity": 10,
  "available": false,
  "message": "Stock insuficiente. Disponible: 4, Solicitado: 10"
}
```

### Test 1.3: Producto inexistente

```bash
grpcurl -plaintext -d '{
  "product_id": 999,
  "quantity": 1
}' localhost:7001 productservice.ProductService/CheckAvailability
```

**Resultado esperado:**
```json
{
  "productId": 999,
  "available": false,
  "message": "Producto 999 no encontrado"
}
```

---

## 📦 PASO 2: Test ReserveStock (Reservar temporalmente)

### Test 2.1: Reservar con stock disponible

```bash
grpcurl -plaintext -d '{
  "product_id": 1,
  "quantity": 3,
  "order_id": 200
}' localhost:7001 productservice.ProductService/ReserveStock
```

**Resultado esperado:**
```json
{
  "success": true,
  "reservationId": "abc-123-def-456...",
  "productId": 1,
  "quantityReserved": 3,
  "message": "Stock reservado exitosamente. Disponible después de reserva: 10",
  "reservedAt": "2025-11-25T20:30:00.000Z"
}
```

**⚠️ IMPORTANTE:** Guardar el `reservationId` para los siguientes tests.

### Test 2.2: Verificar que el stock disponible disminuyó

```bash
grpcurl -plaintext -d '{
  "product_id": 1,
  "quantity": 1
}' localhost:7001 productservice.ProductService/CheckAvailability
```

**Resultado esperado:**
```json
{
  "totalStock": 15,
  "reservedStock": 5,    // <- Aumentó de 2 a 5 (2 originales + 3 nuevas)
  "availableStock": 10   // <- Disminuyó de 13 a 10
}
```

### Test 2.3: Intentar reservar más stock del disponible

```bash
grpcurl -plaintext -d '{
  "product_id": 12,
  "quantity": 10,
  "order_id": 201
}' localhost:7001 productservice.ProductService/ReserveStock
```

**Resultado esperado:**
```json
{
  "success": false,
  "productId": 12,
  "quantityReserved": 0,
  "message": "Stock insuficiente. Disponible: 4, Solicitado: 10"
}
```

### Test 2.4: Verificar en BD

```sql
SELECT * FROM StockReservations 
WHERE OrderId = 200 
AND Status = 'Reserved';
```

Debe mostrar la nueva reserva.

---

## ✅ PASO 3: Test ConfirmReservation (Commit - descuenta stock real)

### Test 3.1: Confirmar la reserva creada en Test 2.1

```bash
# Reemplazar <RESERVATION_ID> con el ID obtenido en Test 2.1
grpcurl -plaintext -d '{
  "reservation_id": "<RESERVATION_ID>"
}' localhost:7001 productservice.ProductService/ConfirmReservation
```

**Resultado esperado:**
```json
{
  "success": true,
  "reservationId": "<RESERVATION_ID>",
  "productId": 1,
  "quantityConfirmed": 3,
  "message": "Reserva confirmada. Stock actual del producto: 12",
  "confirmedAt": "2025-11-25T20:35:00.000Z"
}
```

### Test 3.2: Verificar que el stock REAL disminuyó

```bash
grpcurl -plaintext -d '{
  "product_id": 1
}' localhost:7001 productservice.ProductService/GetProduct
```

**Resultado esperado:**
```json
{
  "productId": 1,
  "stock": 12  // <- Disminuyó de 15 a 12 (descontados permanentemente)
}
```

### Test 3.3: Verificar en BD

```sql
-- Ver que la reserva cambió de estado
SELECT * FROM StockReservations 
WHERE ReservationId = '<RESERVATION_ID>';
-- Status debe ser 'Confirmed'

-- Ver que el stock del producto disminuyó
SELECT Id, Name, Stock 
FROM Products 
WHERE Id = 1;
-- Stock debe ser 12
```

### Test 3.4: Intentar confirmar una reserva inexistente

```bash
grpcurl -plaintext -d '{
  "reservation_id": "fake-id-123"
}' localhost:7001 productservice.ProductService/ConfirmReservation
```

**Resultado esperado:**
```json
{
  "success": false,
  "message": "Reserva no encontrada"
}
```

### Test 3.5: Intentar confirmar la misma reserva dos veces

```bash
# Usar el mismo RESERVATION_ID de Test 3.1
grpcurl -plaintext -d '{
  "reservation_id": "<RESERVATION_ID>"
}' localhost:7001 productservice.ProductService/ConfirmReservation
```

**Resultado esperado:**
```json
{
  "success": false,
  "message": "La reserva no puede ser confirmada. Estado actual: Confirmed"
}
```

---

## 🔄 PASO 4: Test ReleaseReservation (Rollback - libera sin devolver stock)

### Test 4.1: Crear nueva reserva para luego liberarla

```bash
grpcurl -plaintext -d '{
  "product_id": 6,
  "quantity": 2,
  "order_id": 202
}' localhost:7001 productservice.ProductService/ReserveStock
```

**Guardar el nuevo `reservationId`.**

### Test 4.2: Liberar la reserva recién creada

```bash
# Reemplazar <NEW_RESERVATION_ID>
grpcurl -plaintext -d '{
  "reservation_id": "<NEW_RESERVATION_ID>",
  "reason": "Payment failed - Testing rollback"
}' localhost:7001 productservice.ProductService/ReleaseReservation
```

**Resultado esperado:**
```json
{
  "success": true,
  "reservationId": "<NEW_RESERVATION_ID>",
  "productId": 6,
  "quantityReleased": 2,
  "message": "Reserva liberada. El stock está nuevamente disponible.",
  "releasedAt": "2025-11-25T20:40:00.000Z",
  "reason": "Payment failed - Testing rollback"
}
```

### Test 4.3: Verificar que el stock disponible aumentó

```bash
grpcurl -plaintext -d '{
  "product_id": 6,
  "quantity": 1
}' localhost:7001 productservice.ProductService/CheckAvailability
```

**Resultado esperado:**
- `availableStock` debe haber aumentado (la reserva liberada ya no cuenta como "reservada")

### Test 4.4: Verificar en BD

```sql
SELECT * FROM StockReservations 
WHERE ReservationId = '<NEW_RESERVATION_ID>';
```

- Status = 'Released'
- ReleasedAt tiene fecha
- ReleaseReason tiene el texto

**IMPORTANTE:** El Stock del producto NO cambió (sigue igual), solo se liberó la reserva temporal.

### Test 4.5: Intentar liberar una reserva confirmada

```bash
# Usar el RESERVATION_ID que confirmamos en Test 3.1
grpcurl -plaintext -d '{
  "reservation_id": "<RESERVATION_ID>",
  "reason": "Testing invalid release"
}' localhost:7001 productservice.ProductService/ReleaseReservation
```

**Resultado esperado:**
```json
{
  "success": false,
  "message": "La reserva ya fue confirmada y no puede ser liberada"
}
```

---

## 🎯 PASO 5: Test de Flujo Completo (Simular Saga)

### Escenario A: Saga Exitosa

```bash
# 1. Reservar stock
RESERVE_RESULT=$(grpcurl -plaintext -d '{
  "product_id": 2,
  "quantity": 2,
  "order_id": 300
}' localhost:7001 productservice.ProductService/ReserveStock)

echo $RESERVE_RESULT
# Extraer el reservationId manualmente

# 2. Simular que el pago fue exitoso

# 3. Confirmar reserva
grpcurl -plaintext -d '{
  "reservation_id": "<EXTRACTED_RESERVATION_ID>"
}' localhost:7001 productservice.ProductService/ConfirmReservation

# 4. Verificar stock descontado
grpcurl -plaintext -d '{
  "product_id": 2
}' localhost:7001 productservice.ProductService/GetProduct
```

### Escenario B: Saga con Rollback

```bash
# 1. Reservar stock
RESERVE_RESULT=$(grpcurl -plaintext -d '{
  "product_id": 3,
  "quantity": 3,
  "order_id": 301
}' localhost:7001 productservice.ProductService/ReserveStock)

# Extraer reservationId

# 2. Simular que el pago FALLÓ

# 3. Liberar reserva (compensación)
grpcurl -plaintext -d '{
  "reservation_id": "<EXTRACTED_RESERVATION_ID>",
  "reason": "Payment failed - Insufficient funds"
}' localhost:7001 productservice.ProductService/ReleaseReservation

# 4. Verificar que stock NO cambió (solo se liberó la reserva)
grpcurl -plaintext -d '{
  "product_id": 3
}' localhost:7001 productservice.ProductService/GetProduct
```

---

## ✅ CHECKLIST DE VALIDACIÓN

Antes de continuar con la saga completa, verificar:

- [ ] CheckAvailability retorna stock correcto
- [ ] ReserveStock crea reservas y disminuye stock disponible
- [ ] ConfirmReservation descuenta stock real permanentemente
- [ ] ReleaseReservation libera la reserva sin cambiar stock real
- [ ] No se puede confirmar una reserva inexistente
- [ ] No se puede confirmar una reserva ya confirmada
- [ ] No se puede liberar una reserva ya confirmada
- [ ] Los logs muestran información clara de cada operación
- [ ] La BD refleja correctamente todos los cambios

---

## 🐛 TROUBLESHOOTING

### Error: "reservation_id" is not a valid field

Verificar que el proto tenga snake_case:
```protobuf
message ConfirmReservationRequest {
  string reservation_id = 1;  // ← Debe ser snake_case
}
```

### Stock no disminuye

Verificar que:
1. ConfirmReservation esté descontando: `product.Stock -= reservation.QuantityReserved`
2. Se esté llamando a `SaveChangesAsync()`

### Reserva no se crea

Verificar logs para ver el error específico.

---

## 📊 QUERIES ÚTILES PARA DEBUGGING

```sql
-- Ver estado completo de un producto
SELECT 
    p.Id,
    p.Name,
    p.Stock as TotalStock,
    (SELECT COUNT(*) FROM StockReservations WHERE ProductId = p.Id) as TotalReservations,
    (SELECT SUM(QuantityReserved) FROM StockReservations WHERE ProductId = p.Id AND Status = 'Reserved') as ActiveReservations,
    (SELECT SUM(QuantityReserved) FROM StockReservations WHERE ProductId = p.Id AND Status = 'Confirmed') as ConfirmedReservations,
    (SELECT SUM(QuantityReserved) FROM StockReservations WHERE ProductId = p.Id AND Status = 'Released') as ReleasedReservations,
    p.Stock - COALESCE((SELECT SUM(QuantityReserved) FROM StockReservations WHERE ProductId = p.Id AND Status = 'Reserved'), 0) as AvailableStock
FROM Products p
WHERE p.Id = 1;  -- Cambiar ID según necesites

-- Ver historial de reservas de un producto
SELECT 
    ReservationId,
    OrderId,
    QuantityReserved,
    Status,
    CreatedAt,
    ConfirmedAt,
    ReleasedAt,
    ReleaseReason
FROM StockReservations
WHERE ProductId = 1
ORDER BY CreatedAt DESC;

-- Ver todas las reservas activas (pendientes de confirmar)
SELECT 
    sr.ReservationId,
    p.Name as ProductName,
    sr.OrderId,
    sr.QuantityReserved,
    sr.CreatedAt,
    DATEDIFF(MINUTE, sr.CreatedAt, GETUTCDATE()) as MinutesAgo
FROM StockReservations sr
INNER JOIN Products p ON sr.ProductId = p.Id
WHERE sr.Status = 'Reserved'
ORDER BY sr.CreatedAt DESC;
```

---

**Una vez que todos estos tests pasen, estaremos listos para probar la saga completa!** 🚀
-----------------------
ReserveStock: Reserva temporal al iniciar saga
ConfirmReservation: Commit cuando saga completa exitosamente
ReleaseReservation: Rollback cuando saga falla
CheckAvailability: Validación antes de iniciar saga

La tabla StockReservations mantiene un registro de todas las reservas para auditoría.

# Reservar stock
grpcurl -plaintext -d '{
  "product_id": 1,
  "quantity": 2,
  "order_id": 100
}' localhost:7001 productservice.ProductService/ReserveStock

# Verificar disponibilidad
grpcurl -plaintext -d '{
  "product_id": 1,
  "quantity": 5
}' localhost:7001 productservice.ProductService/CheckAvailability

# Confirmar reserva (usar el reservation_id devuelto)
grpcurl -plaintext -d '{
  "reservation_id": "xxxx-xxxx-xxxx"
}' localhost:7001 productservice.ProductService/ConfirmReservation

# Liberar reserva (rollback)
grpcurl -plaintext -d '{
  "reservation_id": "xxxx-xxxx-xxxx",
  "reason": "Payment failed"
}' localhost:7001 productservice.ProductService/ReleaseReservation

--------------------------------
InventoryService.gRPC (?):
ReserveStock(productId, quantity) → ReservationId
ConfirmReservation(reservationId) → Success/Failure (commit)
ReleaseReservation(reservationId) → Success/Failure (rollback)
CheckAvailability(productId) → Available quantity
GetReservation(reservationId) → Reservation details