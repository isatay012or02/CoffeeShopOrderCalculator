using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Windows.Forms;
using OfficeOpenXml;
using System.Linq;

namespace CoffeeShopOrderCalculator
{
    public partial class MainForm : Form
    {
        private SQLiteConnection connection;
        private string dbPath = "CoffeeShopOrders.db";

        // Define products and prices
        private Dictionary<string, decimal> products = new Dictionary<string, decimal>
        {
            { "Кофе", 1000 },
            { "Чай", 800 },
            { "Круассан", 1200 },
            { "Сок", 900 }
        };

        // Dictionary to store NumericUpDown controls for each product
        private Dictionary<string, NumericUpDown> productQuantities = new Dictionary<string, NumericUpDown>();

        public MainForm()
        {
            InitializeComponent();
            InitializeDatabase();

            // Теперь создаем элементы интерфейса для продуктов программно
            PopulateProductList();
        }

        private void PopulateProductList()
        {
            int yPos = 10;

            // Create a header row
            Label nameHeader = new Label
            {
                Text = "Продукт",
                Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold),
                AutoSize = true,
                Location = new System.Drawing.Point(10, yPos)
            };
            productsPanel.Controls.Add(nameHeader);

            Label priceHeader = new Label
            {
                Text = "Цена",
                Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold),
                AutoSize = true,
                Location = new System.Drawing.Point(150, yPos)
            };
            productsPanel.Controls.Add(priceHeader);

            Label quantityHeader = new Label
            {
                Text = "Количество",
                Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold),
                AutoSize = true,
                Location = new System.Drawing.Point(250, yPos)
            };
            productsPanel.Controls.Add(quantityHeader);

            yPos += 30;

            // Add each product with its price and quantity selector
            foreach (var product in products)
            {
                // Product name
                Label nameLabel = new Label
                {
                    Text = product.Key,
                    AutoSize = true,
                    Location = new System.Drawing.Point(10, yPos)
                };
                productsPanel.Controls.Add(nameLabel);

                // Product price
                Label priceLabel = new Label
                {
                    Text = product.Value.ToString() + " тг.",
                    AutoSize = true,
                    Location = new System.Drawing.Point(150, yPos)
                };
                productsPanel.Controls.Add(priceLabel);

                // Quantity numeric updown
                NumericUpDown quantityUpDown = new NumericUpDown
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0,
                    Location = new System.Drawing.Point(250, yPos),
                    Width = 60
                };
                productsPanel.Controls.Add(quantityUpDown);

                // Store the quantity control reference
                productQuantities[product.Key] = quantityUpDown;

                yPos += 35;
            }
        }

        private void InitializeDatabase()
        {
            // Create database if it doesn't exist
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
            }

            // Open connection
            connection = new SQLiteConnection($"Data Source={dbPath};Version=3;");
            connection.Open();

            // Create table if it doesn't exist
            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS Orders (
                    OrderId INTEGER PRIMARY KEY AUTOINCREMENT,
                    OrderDetails TEXT NOT NULL,
                    TotalAmount DECIMAL(10, 2) NOT NULL,
                    OrderDate DATETIME NOT NULL
                );";

            using (SQLiteCommand command = new SQLiteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private void calculateButton_Click(object sender, EventArgs e)
        {
            decimal totalAmount = 0;
            bool hasSelectedProducts = false;
            List<string> orderDetailsList = new List<string>();

            // Calculate the total and check if at least one product is selected
            foreach (var product in products)
            {
                int quantity = (int)productQuantities[product.Key].Value;

                if (quantity > 0)
                {
                    hasSelectedProducts = true;
                    decimal itemTotal = product.Value * quantity;
                    totalAmount += itemTotal;
                    orderDetailsList.Add($"{product.Key}: {quantity}");
                }
            }

            if (!hasSelectedProducts)
            {
                MessageBox.Show("Выберите хотя бы один товар!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                totalLabel.Text = "Итоговая сумма: 0 тг.";
                return;
            }

            totalLabel.Text = $"Итоговая сумма: {totalAmount} тг.";

            // Save order to database
            SaveOrderToDatabase(string.Join(", ", orderDetailsList), totalAmount);
        }

        private void SaveOrderToDatabase(string orderDetails, decimal totalAmount)
        {
            string insertQuery = @"
                INSERT INTO Orders (OrderDetails, TotalAmount, OrderDate)
                VALUES (@OrderDetails, @TotalAmount, @OrderDate);";

            using (SQLiteCommand command = new SQLiteCommand(insertQuery, connection))
            {
                command.Parameters.AddWithValue("@OrderDetails", orderDetails);
                command.Parameters.AddWithValue("@TotalAmount", totalAmount);
                command.Parameters.AddWithValue("@OrderDate", DateTime.Now);

                command.ExecuteNonQuery();
            }

            MessageBox.Show("Заказ успешно сохранен!", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void exportButton_Click(object sender, EventArgs e)
        {
            ExportOrdersToExcel();
        }

        private void ExportOrdersToExcel()
        {
            try
            {
                // Get all orders from database
                DataTable ordersTable = new DataTable();
                string selectQuery = "SELECT * FROM Orders ORDER BY OrderId;";

                using (SQLiteCommand command = new SQLiteCommand(selectQuery, connection))
                {
                    using (SQLiteDataAdapter adapter = new SQLiteDataAdapter(command))
                    {
                        adapter.Fill(ordersTable);
                    }
                }

                if (ordersTable.Rows.Count == 0)
                {
                    MessageBox.Show("Нет заказов для экспорта!", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // ExcelPackage.LicenseContext required for EPPlus version 5+
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                // Save file dialog
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    Title = "Сохранить заказы",
                    FileName = "Заказы_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")
                };

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    using (ExcelPackage package = new ExcelPackage())
                    {
                        ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("Заказы");

                        // Add headers
                        worksheet.Cells[1, 1].Value = "Номер заказа";
                        worksheet.Cells[1, 2].Value = "Список товаров";
                        worksheet.Cells[1, 3].Value = "Сумма (тг.)";
                        worksheet.Cells[1, 4].Value = "Дата и время";

                        // Style headers
                        using (var range = worksheet.Cells[1, 1, 1, 4])
                        {
                            range.Style.Font.Bold = true;
                        }

                        // Add data
                        for (int i = 0; i < ordersTable.Rows.Count; i++)
                        {
                            worksheet.Cells[i + 2, 1].Value = ordersTable.Rows[i]["OrderId"];
                            worksheet.Cells[i + 2, 2].Value = ordersTable.Rows[i]["OrderDetails"];
                            worksheet.Cells[i + 2, 3].Value = ordersTable.Rows[i]["TotalAmount"];
                            worksheet.Cells[i + 2, 4].Value = Convert.ToDateTime(ordersTable.Rows[i]["OrderDate"]);
                        }

                        // Auto-fit columns
                        worksheet.Cells.AutoFitColumns();

                        // Save the file
                        package.SaveAs(new FileInfo(saveDialog.FileName));
                    }

                    MessageBox.Show($"Заказы успешно экспортированы в файл:\n{saveDialog.FileName}",
                                   "Экспорт завершен",
                                   MessageBoxButtons.OK,
                                   MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте в Excel: {ex.Message}",
                               "Ошибка",
                               MessageBoxButtons.OK,
                               MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            // Close the database connection when the form is closing
            if (connection != null && connection.State == ConnectionState.Open)
            {
                connection.Close();
                connection.Dispose();
            }
        }
    }
}