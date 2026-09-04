
// ============================================================
// CREDIT CARD PERCENTAGE
// PAGE SPECIFIC JS
// ============================================================

let allData = [

    {
        id: 1,
        cardType: "Visa",
        percentage: 1.50,
        minimumAmount: 100,
        maximumAmount: 100000,
        status: "Active",
        effectiveDate: "2026-08-01"
    },

    {
        id: 2,
        cardType: "MasterCard",
        percentage: 1.75,
        minimumAmount: 100,
        maximumAmount: 100000,
        status: "Active",
        effectiveDate: "2026-08-01"
    },

    {
        id: 3,
        cardType: "RuPay",
        percentage: 1.25,
        minimumAmount: 50,
        maximumAmount: 50000,
        status: "Active",
        effectiveDate: "2026-08-05"
    },

    {
        id: 4,
        cardType: "American Express",
        percentage: 2.00,
        minimumAmount: 500,
        maximumAmount: 200000,
        status: "Inactive",
        effectiveDate: "2026-07-15"
    },

    {
        id: 5,
        cardType: "Other",
        percentage: 2.25,
        minimumAmount: 100,
        maximumAmount: 100000,
        status: "Active",
        effectiveDate: "2026-08-10"
    }
];


let deleteId = 0;


// ============================================================
// DOCUMENT READY
// ============================================================

$(document).ready(function () {

    // Common table
    initCommonTable({

        pageSize: 10,

        defaultSortColumn: "cardType"

    });


    // Initial table
    renderTable();


    // ========================================================
    // ADD
    // ========================================================

    $("#btnAdd").on("click", function () {

        resetForm();

        $("#percentageFormCard")
            .stop(true, true)
            .slideDown(200);

        scrollToForm();
    });


    // ========================================================
    // CLOSE
    // ========================================================

    $("#btnCloseForm").on("click", function () {

        closeForm();
    });


    // ========================================================
    // CANCEL
    // ========================================================

    $("#btnCancel").on("click", function () {

        closeForm();
    });


    // ========================================================
    // SAVE
    // ========================================================

    $("#percentageForm").on("submit", function (e) {

        e.preventDefault();

        saveRecord();
    });


    // ========================================================
    // DELETE CANCEL
    // ========================================================

    $("#btnDeleteCancel").on("click", function () {

        closeDeleteModal();
    });


    // ========================================================
    // DELETE CONFIRM
    // ========================================================

    $("#btnDeleteConfirm").on("click", function () {

        if (deleteId > 0) {

            deleteRecord(deleteId);
        }
    });


    // ========================================================
    // PRINT
    // ========================================================

    $("#btnPrint").on("click", function () {

        window.print();
    });


    // ========================================================
    // EXCEL
    // ========================================================

    $("#btnExcel").on("click", function () {

        exportExcel();
    });


    // ========================================================
    // CLEAR VALIDATION ERROR
    // ========================================================

    $("#cardType")
        .on("change", function () {

            $("#cardTypeError").text("");
        });


    $("#percentage")
        .on("input", function () {

            $("#percentageError").text("");
        });


    $("#status")
        .on("change", function () {

            $("#statusError").text("");
        });


    $("#effectiveDate")
        .on("change", function () {

            $("#effectiveDateError").text("");
        });

});


// ============================================================
// RENDER TABLE
// ============================================================

// ============================================================
// RENDER TABLE
// ============================================================

function renderTable() {

    // Common search + filter + sorting
    const data = getCommonTableData(allData);

    // Common pagination
    const result = getCommonPageData(data);

    let html = "";

    // ========================================================
    // TABLE ROWS
    // ========================================================

    result.pageData.forEach(function (item, index) {

        const statusClass =
            String(item.status).toLowerCase() === "active"
                ? "status-success"
                : "status-failed";

        html += `
            <tr>

                <td>
                    ${result.startIndex + index + 1}
                </td>

                <td>
                    <strong>
                        ${escapeHtml(item.cardType)}
                    </strong>
                </td>

                <td class="text-right">
                    ${Number(item.percentage).toFixed(2)}%
                </td>

                <td class="text-right">
                    ₹${formatAmount(item.minimumAmount)}
                </td>

                <td class="text-right">
                    ₹${formatAmount(item.maximumAmount)}
                </td>

                <td>
                    <span class="status-badge ${statusClass}">
                        ${escapeHtml(item.status)}
                    </span>
                </td>

                <td>
                    ${formatDate(item.effectiveDate)}
                </td>

                <td class="text-center">

                    <button
                        type="button"
                        class="action-btn action-edit"
                        onclick="editRecord(${item.id})"
                        title="Edit">

                        <i class="bi bi-pencil"></i>

                    </button>

                    <button
                        type="button"
                        class="action-btn action-delete"
                        onclick="openDeleteModal(${item.id})"
                        title="Delete">

                        <i class="bi bi-trash"></i>

                    </button>

                </td>

            </tr>
        `;
    });

    // ========================================================
    // BIND TABLE DATA
    // ========================================================

    $("#percentageTableBody").html(html);

    // ========================================================
    // NO DATA
    // ========================================================

    if (result.totalRecords === 0) {

        $("#noData").show();
        $("#percentageTable").hide();

    } else {

        $("#noData").hide();
        $("#percentageTable").show();
    }

    // ========================================================
    // RECORD INFO
    // ========================================================

    updateRecordInfo(
        result.startIndex,
        result.endIndex,
        result.totalRecords
    );

    // ========================================================
    // PAGINATION
    // ========================================================

    renderPagination(result.totalPages);
}


// ============================================================
// SAVE RECORD
// ============================================================

function saveRecord() {

    clearErrors();


    // ========================================================
    // GET VALUES
    // ========================================================

    const id =
        parseInt(
            $("#percentageId").val(),
            10
        ) || 0;


    const cardType =
        String(
            $("#cardType").val() || ""
        ).trim();


    const percentageValue =
        $("#percentage").val();


    const percentage =
        parseFloat(
            percentageValue
        );


    const minimumValue =
        $("#minimumAmount").val();


    const maximumValue =
        $("#maximumAmount").val();


    const minimumAmount =
        minimumValue === ""
            ? 0
            : parseFloat(minimumValue);


    const maximumAmount =
        maximumValue === ""
            ? 0
            : parseFloat(maximumValue);


    const status =
        $("#status").val();


    const effectiveDate =
        $("#effectiveDate").val();


    let isValid = true;


    // ========================================================
    // CARD TYPE VALIDATION
    // ========================================================

    if (!cardType) {

        showFieldError(
            "cardTypeError",
            "Please select card type."
        );

        isValid = false;
    }


    // ========================================================
    // PERCENTAGE VALIDATION
    // ========================================================

    if (
        percentageValue === "" ||
        isNaN(percentage) ||
        percentage < 0 ||
        percentage > 100
    ) {

        showFieldError(
            "percentageError",
            "Please enter percentage between 0 and 100."
        );

        isValid = false;
    }


    // ========================================================
    // MINIMUM AMOUNT
    // ========================================================

    if (
        minimumValue !== "" &&
        (
            isNaN(minimumAmount) ||
            minimumAmount < 0
        )
    ) {

        showFieldError(
            "percentageError",
            "Please enter a valid minimum amount."
        );

        isValid = false;
    }


    // ========================================================
    // MAXIMUM AMOUNT
    // ========================================================

    if (
        maximumValue !== "" &&
        (
            isNaN(maximumAmount) ||
            maximumAmount < 0
        )
    ) {

        showFieldError(
            "percentageError",
            "Please enter a valid maximum amount."
        );

        isValid = false;
    }


    // ========================================================
    // MINIMUM > MAXIMUM
    // ========================================================

    if (
        minimumValue !== "" &&
        maximumValue !== "" &&
        minimumAmount > maximumAmount
    ) {

        showFieldError(
            "percentageError",
            "Minimum amount cannot be greater than maximum amount."
        );

        isValid = false;
    }


    // ========================================================
    // STATUS
    // ========================================================

    if (!status) {

        showFieldError(
            "statusError",
            "Please select status."
        );

        isValid = false;
    }


    // ========================================================
    // EFFECTIVE DATE
    // ========================================================

    if (!effectiveDate) {

        showFieldError(
            "effectiveDateError",
            "Please select effective date."
        );

        isValid = false;
    }


    if (!isValid) {
        return;
    }


    // ========================================================
    // UPDATE
    // ========================================================

    if (id > 0) {

        const record =
            allData.find(function (item) {

                return item.id === id;

            });


        if (!record) {

            showToast(
                "Record not found."
            );

            return;
        }


        record.cardType =
            cardType;

        record.percentage =
            percentage;

        record.minimumAmount =
            minimumAmount;

        record.maximumAmount =
            maximumAmount;

        record.status =
            status;

        record.effectiveDate =
            effectiveDate;


        showToast(
            "Percentage updated successfully."
        );

    }


    // ========================================================
    // INSERT
    // ========================================================

    else {

        const newId =
            allData.length > 0
                ? Math.max(
                    ...allData.map(function (item) {
                        return item.id;
                    })
                ) + 1
                : 1;


        allData.push({

            id: newId,

            cardType:
                cardType,

            percentage:
                percentage,

            minimumAmount:
                minimumAmount,

            maximumAmount:
                maximumAmount,

            status:
                status,

            effectiveDate:
                effectiveDate
        });


        showToast(
            "Percentage saved successfully."
        );
    }


    // ========================================================
    // AFTER SAVE
    // ========================================================

    resetForm();

    $("#percentageFormCard")
        .stop(true, true)
        .slideUp(200);

    renderTable();
}


// ============================================================
// EDIT RECORD
// ============================================================

function editRecord(id) {

    const record =
        allData.find(function (item) {

            return item.id === id;

        });


    if (!record) {

        showToast(
            "Record not found."
        );

        return;
    }


    $("#percentageId")
        .val(record.id);


    $("#cardType")
        .val(record.cardType);


    $("#percentage")
        .val(record.percentage);


    $("#minimumAmount")
        .val(record.minimumAmount);


    $("#maximumAmount")
        .val(record.maximumAmount);


    $("#status")
        .val(record.status);


    $("#effectiveDate")
        .val(record.effectiveDate);


    clearErrors();


    $("#percentageFormCard")
        .stop(true, true)
        .slideDown(200);


    scrollToForm();
}


// ============================================================
// OPEN DELETE MODAL
// ============================================================

function openDeleteModal(id) {

    const record =
        allData.find(function (item) {

            return item.id === id;

        });


    if (!record) {
        return;
    }


    deleteId = id;


    $("#deleteModal")
        .stop(true, true)
        .fadeIn(200);
}


// ============================================================
// CLOSE DELETE MODAL
// ============================================================

function closeDeleteModal() {

    $("#deleteModal")
        .stop(true, true)
        .fadeOut(200);


    deleteId = 0;
}


// ============================================================
// DELETE RECORD
// ============================================================

function deleteRecord(id) {

    const exists =
        allData.some(function (item) {

            return item.id === id;

        });


    if (!exists) {

        closeDeleteModal();

        showToast(
            "Record not found."
        );

        return;
    }


    allData =
        allData.filter(function (item) {

            return item.id !== id;

        });


    closeDeleteModal();

    renderTable();

    showToast(
        "Percentage deleted successfully."
    );
}


// ============================================================
// CLOSE FORM
// ============================================================

function closeForm() {

    $("#percentageFormCard")
        .stop(true, true)
        .slideUp(200);


    resetForm();
}


// ============================================================
// RESET FORM
// ============================================================

function resetForm() {

    const form =
        $("#percentageForm")[0];


    if (form) {

        form.reset();
    }


    $("#percentageId")
        .val(0);


    clearErrors();
}


// ============================================================
// CLEAR ERRORS
// ============================================================

function clearErrors() {

    $(".field-error")
        .text("");

    $(".form-control")
        .removeClass("input-error")
        .removeClass("is-invalid");
}


// ============================================================
// SHOW FIELD ERROR
// ============================================================

function showFieldError(
    errorId,
    message
) {

    $("#" + errorId)
        .text(message);
}


// ============================================================
// FORMAT DATE
// ============================================================

function formatDate(dateValue) {

    if (!dateValue) {
        return "";
    }


    const value =
        String(dateValue);


    // YYYY-MM-DD
    const parts =
        value.split("-");


    if (parts.length === 3) {

        return (
            parts[2] +
            "-" +
            parts[1] +
            "-" +
            parts[0]
        );
    }


    const date =
        new Date(dateValue);


    if (isNaN(date.getTime())) {

        return value;
    }


    const day =
        String(
            date.getDate()
        ).padStart(2, "0");


    const month =
        String(
            date.getMonth() + 1
        ).padStart(2, "0");


    const year =
        date.getFullYear();


    return (
        day +
        "-" +
        month +
        "-" +
        year
    );
}


// ============================================================
// FORMAT AMOUNT
// ============================================================

function formatAmount(amount) {

    const value =
        Number(amount) || 0;


    return value.toLocaleString(
        "en-IN",
        {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }
    );
}


// ============================================================
// ESCAPE HTML
// ============================================================

function escapeHtml(value) {

    return String(value ?? "")
        .replace(
            /&/g,
            "&amp;"
        )
        .replace(
            /</g,
            "&lt;"
        )
        .replace(
            />/g,
            "&gt;"
        )
        .replace(
            /"/g,
            "&quot;"
        )
        .replace(
            /'/g,
            "&#039;"
        );
}


// ============================================================
// SCROLL TO FORM
// ============================================================

function scrollToForm() {

    const formCard =
        $("#percentageFormCard");


    if (!formCard.length) {
        return;
    }


    const position =
        formCard.offset();


    if (!position) {
        return;
    }


    $("html, body")
        .animate(
            {
                scrollTop:
                    position.top - 20
            },
            400
        );
}


// ============================================================
// TOAST
// ============================================================

function showToast(message) {

    $("#toastMessage")
        .text(message);


    $("#toast")
        .stop(true, true)
        .addClass("show");


    setTimeout(function () {

        $("#toast")
            .removeClass("show");

    }, 2500);
}


// ============================================================
// EXCEL EXPORT
// ============================================================

function exportExcel() {

    const data =
        getCommonTableData(allData);


    exportCommonExcel(

        data,

        [

            {
                field: "sno",
                header: "S.No"
            },

            {
                field: "cardType",
                header: "Card Type"
            },

            {
                field: "percentage",
                header: "Percentage",

                format: function (item) {

                    return (
                        Number(item.percentage)
                            .toFixed(2) +
                        "%"
                    );
                }
            },

            {
                field: "minimumAmount",
                header: "Minimum Amount",

                format: function (item) {

                    return formatAmount(
                        item.minimumAmount
                    );
                }
            },

            {
                field: "maximumAmount",
                header: "Maximum Amount",

                format: function (item) {

                    return formatAmount(
                        item.maximumAmount
                    );
                }
            },

            {
                field: "status",
                header: "Status"
            },

            {
                field: "effectiveDate",
                header: "Effective Date",

                format: function (item) {

                    return formatDate(
                        item.effectiveDate
                    );
                }
            }

        ],

        "CreditCardPercentage.csv"
    );
}
