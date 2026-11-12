using Moq;
using Xunit;
using Order_Project.Models;
using Order_Project.Services;
using Order_Project.Services.Intefraces;

namespace Order_Project_Tests
{
    public class OrderServiceTests
    {
        private readonly Mock<IInventoryService> _inventoryMock;
        private readonly Mock<IPaymentService> _paymentMock;
        private readonly Mock<INotificationService> _notificationMock;
        private readonly OrderService _service;

        public OrderServiceTests()
        {
            _inventoryMock = new Mock<IInventoryService>();
            _paymentMock = new Mock<IPaymentService>();
            _notificationMock = new Mock<INotificationService>();

            _service = new OrderService(_inventoryMock.Object, _paymentMock.Object, _notificationMock.Object);
        }
        private const string DefaultProduct = "Laptop";
        private const int DefaultQuantity = 2;
        /// <summary>
        /// Перевіряє блок валідації вхідних даних при створенні замовлення.
        /// Очікується викидання винятку ArgumentException, якщо передано порожню назву товару, 
        /// нульову або від'ємну кількість. Використовує параметризацію для тестування кількох сценаріїв.
        /// </summary>
        [Theory]
        [InlineData("", 5)]
        [InlineData(null, 5)]
        [InlineData(DefaultProduct, 0)]
        [InlineData(DefaultProduct, -10)]
        public void CreateOrder_InvalidInput_ThrowsArgumentException(string product, int quantity)
        {
            // Act
            Action actual = () => _service.CreateOrder(product, quantity);

            // Assert
            Assert.Throws<ArgumentException>(actual);
        }
        /// <summary>
        /// Перевіряє успішне створення замовлення при коректних вхідних даних.
        /// Гарантує, що метод повертає ініціалізований об'єкт (не null), і що замовлена кількість 
        /// точно відповідає тій, яка була передана у метод.
        /// </summary>
        [Fact]
        public void CreateOrder_ValidInput_ReturnsExpectedQuantity()
        {
            // Arrange
            _inventoryMock.Setup(i => i.CheckStock(DefaultProduct, DefaultQuantity)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            // Act
            var result = _service.CreateOrder(DefaultProduct, DefaultQuantity);

            // Assert
            Assert.Equal(DefaultQuantity, result.Quantity);
        }
        /// <summary>
        /// Перевіряє логіку генерації ідентифікаторів для нових замовлень.
        /// Створює два різні замовлення підряд та переконується, що їхні ідентифікатори є унікальними 
        /// і не збігаються між собою.
        /// </summary>
        [Fact]
        public void CreateOrder_MultipleOrders_AssignsDifferentIds()
        {
            // Arrange
            const string SecondProduct = "Mouse";
            _inventoryMock.Setup(i => i.CheckStock(It.IsAny<string>(), It.IsAny<int>())).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            // Act
            var order1 = _service.CreateOrder(DefaultProduct, 1);
            var order2 = _service.CreateOrder(SecondProduct, 1);

            // Assert
            Assert.NotEqual(order1.Id, order2.Id);
        }
        /// <summary>
        /// Перевіряє можливість успішного оновлення наявного замовлення.
        /// Гарантує, що при передачі правильного Id та валідної нової кількості товару, 
        /// метод виконує оновлення і повертає логічне true.
        /// </summary>
        [Fact]
        public void UpdateOrder_ValidUpdate_ReturnsTrue()
        {
            // Arrange
            const int NewQuantity = 5;
            _inventoryMock.Setup(i => i.CheckStock(DefaultProduct, DefaultQuantity)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);
            var order = _service.CreateOrder(DefaultProduct, DefaultQuantity);

            // Act
            var result = _service.UpdateOrder(order.Id, NewQuantity);

            // Assert
            Assert.True(result);
        }
        /// <summary>
        /// Перевіряє захисні механізми системи при спробі видалити замовлення, якого не існує.
        /// Очікується, що метод поверне логічне false замість того, щоб викликати помилку програми.
        /// </summary>
        [Fact]
        public void RemoveOrder_NonExistentId_ReturnsFalse()
        {
            // Arrange
            const int NewQuantity = 5;
            _inventoryMock.Setup(i => i.CheckStock(DefaultProduct, DefaultQuantity)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);
            var order = _service.CreateOrder(DefaultProduct, DefaultQuantity);

            // Act
            var result = _service.RemoveOrder(0);

            // Assert
            Assert.False(result);
        }
        /// <summary>
        /// Перевіряє початковий стан внутрішньої колекції замовлень.
        /// Гарантує, що одразу після ініціалізації сервісу (до створення будь-яких замовлень) 
        /// список замовлень залишається абсолютно порожнім.
        /// </summary>
        [Fact]
        public void GetOrders_Initially_ReturnsEmptyCollection()
        {
            // Arrange
            var emptyService = new OrderService(_inventoryMock.Object, _paymentMock.Object, _notificationMock.Object);

            // Act
            var orders = emptyService.GetOrders();

            // Assert
            Assert.Empty(orders);
        }
        /// <summary>
        /// Перевіряє зміну стану колекції після успішного оформлення замовлення.
        /// Переконується, що список більше не є порожнім і містить саме те замовлення, 
        /// яке щойно було згенероване системою.
        /// </summary>
        [Fact]
        public void GetOrders_AfterCreation_ContainsNewOrder()
        {
            // Arrange
            _inventoryMock.Setup(i => i.CheckStock(DefaultProduct, DefaultQuantity)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);
            var expectedOrder = _service.CreateOrder(DefaultProduct, DefaultQuantity);

            // Act
            var orders = _service.GetOrders();

            // Assert
            Assert.Contains(expectedOrder, orders);
        }
        /// <summary>
        /// Перевіряє поведінку системи у випадку невдалої транзакції оплати.
        /// Гарантує, що якщо платіж не пройшов (ProcessPayment повернув false), 
        /// клієнту за жодних обставин не буде надіслано лист із підтвердженням замовлення.
        /// </summary>
        [Fact]
        public void CreateOrder_PaymentFailed_NeverSendsNotification()
        {
            // Arrange
            _inventoryMock.Setup(i => i.CheckStock(DefaultProduct, DefaultQuantity)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(false);

            // Act
            try { _service.CreateOrder(DefaultProduct, DefaultQuantity); }
            catch { }

            // Assert
            _notificationMock.Verify(n => n.SendConfirmation(It.IsAny<Order>()), Times.Never);
        }
        /// <summary>
        /// Перевіряє точність даних, які передаються до сервісу сповіщень.
        /// Використовує перевірку з предикатом, щоб переконатися, що клієнту 
        /// надсилається лист, який містить правильну назву придбаного товару та його кількість.
        /// </summary>
        [Fact]
        public void CreateOrder_ValidOrder_SendsNotificationWithCorrectData()
        {
            // Arrange
            _inventoryMock.Setup(i => i.CheckStock(DefaultProduct, DefaultQuantity)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            // Act
            _service.CreateOrder(DefaultProduct, DefaultQuantity);

            // Assert
            _notificationMock.Verify(n => n.SendConfirmation(It.Is<Order>(
                o => o.Product == DefaultProduct && o.Quantity == DefaultQuantity)));
        }
        /// <summary>
        /// Перевіряє логіку повернення товарів на склад після видалення замовлення.
        /// Суворо контролює, щоб метод збільшення залишків був викликаний 
        /// рівно один раз, що запобігає дублюванню товарів.
        /// </summary>
        [Fact]
        public void RemoveOrder_ExistingOrder_CallsIncreaseStockExactlyOnce()
        {
            _inventoryMock.Setup(i => i.CheckStock(DefaultProduct, DefaultQuantity)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);
            var order = _service.CreateOrder(DefaultProduct, DefaultQuantity);

            _service.RemoveOrder(order.Id);

            _inventoryMock.Verify(i => i.IncreaseStock(DefaultProduct, DefaultQuantity), Times.Exactly(1));
        }
    }

}