
// ============================================================
// COMMON TABLE JS
// Search + Sort + Pagination + Page Size + Status + Excel
// ============================================================

let currentPage = 1;
let pageSize = 10;
let sortColumn = "";
let sortDirection = "asc";


// ============================================================
// INITIALIZE COMMON TABLE
// ============================================================

function initCommonTable(options = {}) {

    currentPage = 1;

    pageSize = options.pageSize || 10;

    sortColumn = options.defaultSortColumn || "";

    sortDirection = "asc";


    // ========================================================
    // SET PAGE SIZE
    // ========================================================

    $("#pageSize").val(pageSize);


    // ========================================================
    // SEARCH
    // ========================================================

    $("#commonSearch")
        .off("keyup.commonTable")
        .on("keyup.commonTable", function () {

            currentPage = 1;

            if (typeof renderTable === "function") {
                renderTable();
            }
        });


    // ========================================================
    // STATUS FILTER
    // ========================================================

    $("#statusFilter")
        .off("change.commonTable")
        .on("change.commonTable", function () {

            currentPage = 1;

            if (typeof renderTable === "function") {
                renderTable();
            }
        });


    // ========================================================
    // PAGE SIZE
    // ========================================================

    $("#pageSize")
        .off("change.commonTable")
        .on("change.commonTable", function () {

            pageSize =
                parseInt($(this).val(), 10) || 10;

            currentPage = 1;

            if (typeof renderTable === "function") {
                renderTable();
            }
        });


    // ========================================================
    // CLEAR SEARCH
    // ========================================================

    $("#btnClearSearch")
        .off("click.commonTable")
        .on("click.commonTable", function () {

            $("#commonSearch").val("");

            $("#statusFilter").val("");

            currentPage = 1;

            if (typeof renderTable === "function") {
                renderTable();
            }
        });


    // ========================================================
    // SORTING
    // ========================================================

    $(document)
        .off("click.commonTable", "th.sortable")
        .on(
            "click.commonTable",
            "th.sortable",
            function () {

                const column =
                    $(this).data("column");

                if (!column) {
                    return;
                }


                // Same column = toggle direction
                if (sortColumn === column) {

                    sortDirection =
                        sortDirection === "asc"
                            ? "desc"
                            : "asc";

                } else {

                    sortColumn = column;

                    sortDirection = "asc";
                }


                currentPage = 1;


                if (typeof renderTable === "function") {
                    renderTable();
                }
            }
        );
}


// ============================================================
// FILTER DATA
// ============================================================

function getCommonFilteredData(data) {

    let result =
        Array.isArray(data)
            ? [...data]
            : [];


    // ========================================================
    // SEARCH
    // ========================================================

    const search =
        String(
            $("#commonSearch").val() || ""
        )
            .trim()
            .toLowerCase();


    if (search !== "") {

        result =
            result.filter(function (item) {

                return Object.keys(item).some(
                    function (key) {

                        const value = item[key];


                        if (
                            value === null ||
                            value === undefined
                        ) {
                            return false;
                        }


                        return String(value)
                            .toLowerCase()
                            .includes(search);
                    }
                );
            });
    }


    // ========================================================
    // STATUS FILTER
    // ========================================================

    const status =
        String(
            $("#statusFilter").val() || ""
        )
            .trim()
            .toLowerCase();


    if (status !== "") {

        result =
            result.filter(function (item) {

                return String(
                    item.status || ""
                )
                    .toLowerCase() === status;
            });
    }


    return result;
}


// ============================================================
// SORT DATA
// ============================================================

function sortCommonData(data) {

    if (!sortColumn) {
        return data;
    }


    return data.sort(function (a, b) {

        let valueA = a[sortColumn];

        let valueB = b[sortColumn];


        // ====================================================
        // NULL HANDLING
        // ====================================================

        if (
            valueA === null ||
            valueA === undefined
        ) {
            valueA = "";
        }


        if (
            valueB === null ||
            valueB === undefined
        ) {
            valueB = "";
        }


        // ====================================================
        // NUMERIC SORT
        // ====================================================

        const numberA =
            Number(valueA);

        const numberB =
            Number(valueB);


        const bothNumeric =
            valueA !== "" &&
            valueB !== "" &&
            !isNaN(numberA) &&
            !isNaN(numberB);


        if (bothNumeric) {

            valueA = numberA;

            valueB = numberB;

        } else {

            // ================================================
            // STRING SORT
            // ================================================

            valueA =
                String(valueA)
                    .toLowerCase();

            valueB =
                String(valueB)
                    .toLowerCase();
        }


        // ====================================================
        // COMPARE
        // ====================================================

        if (valueA < valueB) {

            return sortDirection === "asc"
                ? -1
                : 1;
        }


        if (valueA > valueB) {

            return sortDirection === "asc"
                ? 1
                : -1;
        }


        return 0;
    });
}


// ============================================================
// GET FILTERED + SORTED DATA
// ============================================================

function getCommonTableData(data) {

    let result =
        getCommonFilteredData(data);


    result =
        sortCommonData(result);


    return result;
}


// ============================================================
// PAGINATION DATA
// ============================================================

function getCommonPageData(data) {

    const totalRecords =
        Array.isArray(data)
            ? data.length
            : 0;


    const totalPages =
        Math.ceil(
            totalRecords / pageSize
        );


    // ========================================================
    // CORRECT CURRENT PAGE
    // ========================================================

    if (
        totalPages > 0 &&
        currentPage > totalPages
    ) {

        currentPage = totalPages;
    }


    if (totalPages === 0) {

        currentPage = 1;
    }


    // ========================================================
    // START INDEX
    // ========================================================

    const startIndex =
        (currentPage - 1) * pageSize;


    // ========================================================
    // END INDEX
    // ========================================================

    const endIndex =
        Math.min(
            startIndex + pageSize,
            totalRecords
        );


    // ========================================================
    // PAGE DATA
    // ========================================================

    const pageData =
        data.slice(
            startIndex,
            endIndex
        );


    return {

        pageData: pageData,

        totalRecords: totalRecords,

        totalPages: totalPages,

        startIndex: startIndex,

        endIndex: endIndex
    };
}


// ============================================================
// RENDER PAGINATION
// Previous | 1 | 2 | 3 | Next
// ============================================================

function renderPagination(totalPages) {

    let html = "";


    // ========================================================
    // NO PAGINATION
    // ========================================================

    if (totalPages <= 1) {

        $("#pagination").html("");

        return;
    }


    // ========================================================
    // PREVIOUS BUTTON
    // ========================================================

    html += `
    < button
type = "button"
class="page-btn"
            ${ currentPage === 1 ? "disabled" : "" }
onclick = "changePage(${currentPage - 1})" >

    <i class="bi bi-chevron-left"></i>
Previous

        </button >
    `;


    // ========================================================
    // PAGE NUMBERS
    // ========================================================

    for (
        let i = 1;
        i <= totalPages;
        i++
    ) {

        html += `
    < button
type = "button"
class="page-btn ${i === currentPage ? "active" : ""}"
onclick = "changePage(${i})" >

    ${ i }

            </button >
    `;
    }


    // ========================================================
    // NEXT BUTTON
    // ========================================================

    html += `
    < button
type = "button"
class="page-btn"
            ${ currentPage === totalPages ? "disabled" : "" }
onclick = "changePage(${currentPage + 1})" >

    Next
    < i class="bi bi-chevron-right" ></i >

        </button >
    `;


    // ========================================================
    // DISPLAY PAGINATION
    // ========================================================

    $("#pagination").html(html);
}


// ============================================================
// CHANGE PAGE
// ============================================================

window.changePage = function (page) {

    const filteredData =
        typeof allData !== "undefined" &&
        Array.isArray(allData)
            ? getCommonTableData(allData)
            : [];


    const totalPages =
        Math.ceil(
            filteredData.length / pageSize
        );


    // ========================================================
    // INVALID PAGE
    // ========================================================

    if (
        page < 1 ||
        page > totalPages
    ) {
        return;
    }


    // ========================================================
    // SET CURRENT PAGE
    // ========================================================

    currentPage = page;


    // ========================================================
    // RE-RENDER TABLE
    // ========================================================

    if (typeof renderTable === "function") {

        renderTable();
    }
};


// ============================================================
// RECORD INFORMATION
// ============================================================

function updateRecordInfo(
    startIndex,
    endIndex,
    totalRecords
) {

    if (totalRecords === 0) {

        $("#recordInfo")
            .text(
                "Showing 0 to 0 of 0 entries"
            );

        return;
    }


    $("#recordInfo")
        .text(
            `Showing ${ startIndex + 1 } to ${ endIndex } of ${ totalRecords } entries`
        );
}


// ============================================================
// EXCEL / CSV EXPORT
// ============================================================

function exportCommonExcel(
    data,
    columns,
    fileName
) {

    if (
        !Array.isArray(data) ||
        data.length === 0
    ) {

        alert(
            "No records available to export."
        );

        return;
    }


    let csv = "";


    // ========================================================
    // HEADER
    // ========================================================

    csv +=
        columns
            .map(function (column) {

                return `"${csvEscape(column.header)}"`;

            })
            .join(",") +
        "\n";


    // ========================================================
    // DATA
    // ========================================================

    data.forEach(
        function (item, index) {

            const row =
                columns.map(
                    function (column) {

                        let value;


                        // ====================================
                        // S.NO
                        // ====================================

                        if (
                            column.field === "sno"
                        ) {

                            value =
                                index + 1;
                        }


                        // ====================================
                        // CUSTOM FORMATTER
                        // ====================================

                        else if (
                            typeof column.format ===
                            "function"
                        ) {

                            value =
                                column.format(
                                    item,
                                    index
                                );
                        }


                        // ====================================
                        // NORMAL FIELD
                        // ====================================

                        else {

                            value =
                                item[column.field];
                        }


                        // ====================================
                        // NULL HANDLING
                        // ====================================

                        if (
                            value === null ||
                            value === undefined
                        ) {

                            value = "";
                        }


                        return `"${csvEscape(value)}"`;
                    }
                );


            csv +=
                row.join(",") +
                "\n";
        }
    );


    // ========================================================
    // DOWNLOAD
    // ========================================================

    const blob =
        new Blob(
            [csv],
            {
                type:
                    "text/csv;charset=utf-8;"
            }
        );


    const url =
        URL.createObjectURL(blob);


    const link =
        document.createElement("a");


    link.href = url;


    link.download =
        fileName ||
        "Export.csv";


    document.body.appendChild(link);


    link.click();


    document.body.removeChild(link);


    URL.revokeObjectURL(url);
}


// ============================================================
// CSV ESCAPE
// ============================================================

function csvEscape(value) {

    return String(
        value ?? ""
    )
        .replace(
            /"/g,
            '""'
        );
}

