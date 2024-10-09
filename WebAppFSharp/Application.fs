module Application

let setSpreadsheetGearLicense() =
        try
            SpreadsheetGear.Factory.SetSignedLicense("")
        with
        | ex when ex.Message.Contains("must be the first method called in the Factory class") ->()
        | ex -> eprintfn $"error signing license %s{ex.Message}"
        
module ConvertToExcel =
    open Domain
    open SpreadsheetGear
    open System.Collections.Generic
    open System.IO
// Function to read an Excel template file
    let readExcelFile (filePath: string) : IWorkbook =
        let workbook = Factory.GetWorkbook(filePath)
        workbook
    
    // Function to write sales data to the worksheet
    let lastHeaderRow = 2;
    let writeData (worksheet: IWorksheet) (sales: List<Sale>) =
        for i in 0 .. sales.Count - 1 do
            let sale = sales.[i]
            let row = (+) i lastHeaderRow
            worksheet.Cells.[row, 0].Value <- i + 1
            worksheet.Cells.[row, 1].Value <- sale.employeeId
            worksheet.Cells.[row, 2].Value <- sale.revenue
            worksheet.Cells.[row, 3].Value <- sale.cost
            worksheet.Cells.[row, 4].Value <- sale.profit
            
            // Check if this is the last item then delete the next row
            if (i = sales.Count - 1) then
                let entireRow = worksheet.Cells.[row + 1, 0]
                entireRow.EntireRow.Delete()
            // Insert a new row
            else
                let row = worksheet.Cells.[row + 1, 0, row + 1, 4]
                row.EntireRow.Insert(InsertShiftDirection.Down)
          
        
        let lastDataRow = sales.Count + 1;
        
        // Style data range
        let dataCellRange = worksheet.Cells.[1, 0, lastDataRow + 1, 4]
        dataCellRange.Columns.AutoFit()

    // Function to create a sales chart in the worksheet
    let createChart (worksheet: IWorksheet) (sales: List<Sale>) =
        let windowInfo = worksheet.WindowInfo

        // Set the chart's position and size
        let left = windowInfo.ColumnToPoints(6.)
        let top = windowInfo.RowToPoints(2.)

        // Add a chart shape
        let chartShape = worksheet.Shapes.AddChart(left, top, 500, 300)
        let chart = chartShape.Chart

        // Set the chart's source data
        let dataSourceRange = worksheet.Cells.[1, 1, sales.Count + 1 , 4]
        chart.SetSourceData(dataSourceRange, SpreadsheetGear.Charts.RowCol.Columns)

        // Set the chart type
        chart.ChartType <- SpreadsheetGear.Charts.ChartType.ColumnClustered

        // Add a chart title
        chart.HasTitle <- true
        chart.ChartTitle.Text <- "Sales Report"
        chart.ChartTitle.Font.Size <- 12

        // Configure legend
        chart.Legend.Position <- SpreadsheetGear.Charts.LegendPosition.Bottom
        chart.Legend.Font.Bold <- true
        
    // Function to write sales data to an Excel file
    let generateExcelFileAsync
        (sales: List<Sale>)
        (excelTemplatePath: string) : Async<Stream> =
        async {
            // Set spread sheet gear license
            setSpreadsheetGearLicense()
            
            // Read template file
            let workbook = readExcelFile excelTemplatePath
            let worksheet = workbook.Worksheets.[0]
            
            // Write data to the worksheet
            writeData worksheet sales
            
            // Create a chart to the worksheet
            createChart worksheet sales
            
            // Save the workbook directly to the stream
            let stream = new MemoryStream()
            workbook.SaveToStream(stream, FileFormat.OpenXMLWorkbook)
            // Reset the stream position to the beginning before returning
            stream.Position <- 0L
            
            return stream
        }
    
    