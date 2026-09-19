using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zedex.Application.Common;
using Zedex.Domain.Entities;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;
using Zedex.Web.Models;
using Zedex.Web.Services;

namespace Zedex.Web.Controllers;

/// <summary>
/// Employee records (with private photo / CNIC scans) and their money movements:
/// advances, salary payouts (advance recovered by deduction) and bonuses.
/// Cash out per transaction = NetPaid. Outstanding advance = Σ advances − Σ deducted.
/// </summary>
[Authorize(Policy = "Module:Employees")]
public partial class EmployeesController : Controller
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string UploadFolder = "employees";
    private const int PageSize = 20;

    private readonly AppDbContext _db;
    private readonly IPrivateFileStore _files;
    private readonly IReportExportService _export;

    public EmployeesController(AppDbContext db, IPrivateFileStore files, IReportExportService export)
    {
        _db = db;
        _files = files;
        _export = export;
    }

    // =========================================================
    // List
    // =========================================================

    public async Task<IActionResult> Index(string? search, bool showInactive = false, int page = 1)
    {
        var query = _db.Employees.AsNoTracking();
        if (!showInactive)
            query = query.Where(e => e.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(e => EF.Functions.ILike(e.Name, pattern) ||
                                     (e.Phone != null && EF.Functions.ILike(e.Phone, pattern)) ||
                                     (e.Cnic != null && EF.Functions.ILike(e.Cnic, pattern)) ||
                                     (e.Designation != null && EF.Functions.ILike(e.Designation, pattern)));
        }

        var total = await query.CountAsync();
        page = Math.Max(1, page);
        var items = await query
            .OrderByDescending(e => e.IsActive).ThenBy(e => e.Name)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(e => new EmployeeListItemViewModel
            {
                Id = e.Id,
                Name = e.Name,
                Phone = e.Phone,
                Cnic = e.Cnic,
                Designation = e.Designation,
                PayType = e.PayType,
                PayRate = e.PayRate,
                IsActive = e.IsActive,
                HasPhoto = e.PhotoPath != null,
                AdvanceBalance =
                    (e.Transactions.Where(t => !t.IsDeleted && t.Type == EmployeeTransactionType.Advance).Sum(t => (decimal?)t.NetPaid) ?? 0)
                    - (e.Transactions.Where(t => !t.IsDeleted).Sum(t => (decimal?)t.AdvanceDeducted) ?? 0)
            })
            .ToListAsync();

        var outstanding =
            (await _db.EmployeeTransactions.Where(t => t.Type == EmployeeTransactionType.Advance).SumAsync(t => (decimal?)t.NetPaid) ?? 0)
            - (await _db.EmployeeTransactions.SumAsync(t => (decimal?)t.AdvanceDeducted) ?? 0);

        return View(new EmployeeListViewModel
        {
            Search = search,
            ShowInactive = showInactive,
            TotalAdvanceOutstanding = outstanding,
            Items = new PagedResult<EmployeeListItemViewModel>
            {
                Items = items, Page = page, PageSize = PageSize, TotalCount = total
            }
        });
    }

    // =========================================================
    // Profile + statement
    // =========================================================

    public async Task<IActionResult> Details(int id, DateTime? from, DateTime? to)
    {
        var e = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e is null)
            return NotFound();

        // Default statement range: last 3 months.
        var t = (to ?? DateTime.Today).Date;
        var f = (from ?? t.AddMonths(-3).AddDays(1)).Date;
        if (t < f) (f, t) = (t, f);

        var vm = await BuildStatementAsync(e, f, t);
        vm.NewAdvance = new EmployeeAdvanceFormViewModel { EmployeeId = e.Id };
        return View(vm);
    }

    public async Task<IActionResult> StatementExcel(int id, DateTime? from, DateTime? to)
    {
        var e = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        var (f, t) = Range(from, to);
        var vm = await BuildStatementAsync(e, f, t);
        var bytes = _export.ToExcel($"Employee Statement — {e.Name}", Subtitle(f, t),
            new[] { "Date", "Type", "Period", "Gross Salary", "Advance Given", "Advance Deducted", "Cash Paid", "Paid From", "Advance Balance", "Remarks" },
            StatementRows(vm));
        return File(bytes, ExcelContentType, $"employee-{e.Id}-{f:yyyyMMdd}-{t:yyyyMMdd}.xlsx");
    }

    public async Task<IActionResult> StatementPdf(int id, DateTime? from, DateTime? to)
    {
        var e = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        var (f, t) = Range(from, to);
        var vm = await BuildStatementAsync(e, f, t);
        var bytes = _export.ToPdf($"Employee Statement — {e.Name}", Subtitle(f, t),
            new[] { "Date", "Type", "Period", "Gross", "Adv. Given", "Adv. Deducted", "Paid", "From", "Adv. Bal", "Remarks" },
            StatementRows(vm));
        return File(bytes, "application/pdf", $"employee-{e.Id}-{f:yyyyMMdd}-{t:yyyyMMdd}.pdf");
    }

    private static IEnumerable<object?[]> StatementRows(EmployeeDetailsViewModel vm)
    {
        yield return new object?[] { vm.From, "Opening advance balance", null, null, null, null, null, null, vm.OpeningAdvanceBalance, null };
        foreach (var r in vm.Rows)
            yield return new object?[]
            {
                r.Date, r.TypeLabel,
                r.PeriodFrom is null ? null : $"{r.PeriodFrom:dd MMM} – {r.PeriodTo:dd MMM yyyy}",
                r.GrossAmount, r.AdvanceGiven, r.AdvanceDeducted, r.NetPaid,
                r.PaymentSource.ToString(), r.AdvanceBalance, r.Remarks
            };
        yield return new object?[] { null, "Totals", null, vm.PeriodGross, vm.PeriodAdvanceGiven, vm.PeriodDeducted, vm.PeriodNetPaid, null, null, null };
    }

    private async Task<EmployeeDetailsViewModel> BuildStatementAsync(Employee e, DateTime from, DateTime to)
    {
        var all = _db.EmployeeTransactions.AsNoTracking().Where(x => x.EmployeeId == e.Id);

        var opening =
            (await all.Where(x => x.TransactionDate < from && x.Type == EmployeeTransactionType.Advance).SumAsync(x => (decimal?)x.NetPaid) ?? 0)
            - (await all.Where(x => x.TransactionDate < from).SumAsync(x => (decimal?)x.AdvanceDeducted) ?? 0);

        var current =
            (await all.Where(x => x.Type == EmployeeTransactionType.Advance).SumAsync(x => (decimal?)x.NetPaid) ?? 0)
            - (await all.SumAsync(x => (decimal?)x.AdvanceDeducted) ?? 0);

        var lastSalaryTo = await all
            .Where(x => x.Type == EmployeeTransactionType.SalaryPayment)
            .MaxAsync(x => x.PeriodTo);

        var txns = await all
            .Where(x => x.TransactionDate >= from && x.TransactionDate < to.AddDays(1))
            .OrderBy(x => x.TransactionDate).ThenBy(x => x.Id)
            .ToListAsync();

        var running = opening;
        var rows = new List<EmployeeStatementRowViewModel>();
        foreach (var x in txns)
        {
            var advanceGiven = x.Type == EmployeeTransactionType.Advance ? x.NetPaid : 0;
            running += advanceGiven - x.AdvanceDeducted;
            rows.Add(new EmployeeStatementRowViewModel
            {
                Id = x.Id,
                Date = x.TransactionDate,
                Type = x.Type,
                PeriodFrom = x.PeriodFrom,
                PeriodTo = x.PeriodTo,
                GrossAmount = x.GrossAmount,
                AdvanceGiven = advanceGiven,
                AdvanceDeducted = x.AdvanceDeducted,
                NetPaid = x.NetPaid,
                PaymentSource = x.PaymentSource,
                Remarks = x.Remarks,
                CreatedBy = x.CreatedBy,
                CreatedDate = x.CreatedDate,
                AdvanceBalance = running
            });
        }

        return new EmployeeDetailsViewModel
        {
            Id = e.Id,
            Name = e.Name,
            FatherName = e.FatherName,
            Phone = e.Phone,
            Address = e.Address,
            Cnic = e.Cnic,
            Designation = e.Designation,
            JoiningDate = e.JoiningDate,
            PayType = e.PayType,
            PayRate = e.PayRate,
            IsActive = e.IsActive,
            Remarks = e.Remarks,
            HasPhoto = e.PhotoPath is not null,
            HasCnicFront = e.CnicFrontPath is not null,
            HasCnicBack = e.CnicBackPath is not null,
            LastSalaryPeriodTo = lastSalaryTo,
            From = from,
            To = to,
            OpeningAdvanceBalance = opening,
            CurrentAdvanceBalance = current,
            Rows = rows
        };
    }

    // =========================================================
    // Create / Edit / Delete / Activate
    // =========================================================

    [HttpGet]
    public IActionResult Create() => View(new EmployeeFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmployeeFormViewModel vm)
    {
        vm.Cnic = await ValidateCnicAsync(vm.Cnic, null);
        var files = await SaveFilesAsync(vm);
        if (!ModelState.IsValid)
            return View(vm);

        var employee = new Employee();
        Apply(employee, vm, files);
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Employee \"{employee.Name}\" added.";
        return RedirectToAction(nameof(Details), new { id = employee.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var e = await _db.Employees.FindAsync(id);
        if (e is null || e.IsDeleted)
            return NotFound();

        return View(new EmployeeFormViewModel
        {
            Id = e.Id,
            Name = e.Name,
            FatherName = e.FatherName,
            Phone = e.Phone,
            Address = e.Address,
            Cnic = e.Cnic,
            Designation = e.Designation,
            JoiningDate = e.JoiningDate,
            PayType = e.PayType,
            PayRate = e.PayRate,
            IsActive = e.IsActive,
            Remarks = e.Remarks,
            HasPhoto = e.PhotoPath is not null,
            HasCnicFront = e.CnicFrontPath is not null,
            HasCnicBack = e.CnicBackPath is not null
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EmployeeFormViewModel vm)
    {
        var e = await _db.Employees.FindAsync(vm.Id);
        if (e is null || e.IsDeleted)
            return NotFound();

        vm.Cnic = await ValidateCnicAsync(vm.Cnic, e.Id);
        var files = await SaveFilesAsync(vm);
        if (!ModelState.IsValid)
        {
            vm.HasPhoto = e.PhotoPath is not null;
            vm.HasCnicFront = e.CnicFrontPath is not null;
            vm.HasCnicBack = e.CnicBackPath is not null;
            return View(vm);
        }

        Apply(e, vm, files);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Employee \"{e.Name}\" updated.";
        return RedirectToAction(nameof(Details), new { id = e.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = DbSeeder.AdminRole)]
    public async Task<IActionResult> Delete(int id)
    {
        var e = await _db.Employees.FindAsync(id);
        if (e is null || e.IsDeleted)
            return NotFound();

        if (await _db.EmployeeTransactions.AnyAsync(t => t.EmployeeId == id))
        {
            TempData["Error"] = $"\"{e.Name}\" has salary/advance history and cannot be deleted. Mark the employee inactive instead.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.Employees.Remove(e); // soft delete
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Employee \"{e.Name}\" deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var e = await _db.Employees.FindAsync(id);
        if (e is null || e.IsDeleted)
            return NotFound();
        e.IsActive = !e.IsActive;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"\"{e.Name}\" marked {(e.IsActive ? "active" : "inactive")}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Streams a private employee file. kind = photo | cnic-front | cnic-back.</summary>
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Document(int id, string kind)
    {
        var e = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e is null)
            return NotFound();

        var path = kind switch
        {
            "photo" => e.PhotoPath,
            "cnic-front" => e.CnicFrontPath,
            "cnic-back" => e.CnicBackPath,
            _ => null
        };
        var file = _files.Resolve(path);
        if (file is null)
            return NotFound();
        return PhysicalFile(file.Value.PhysicalPath, file.Value.ContentType);
    }

    // =========================================================
    // Money: advance / bonus
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAdvance(EmployeeAdvanceFormViewModel vm)
    {
        var e = await _db.Employees.FindAsync(vm.EmployeeId);
        if (e is null || e.IsDeleted)
            return NotFound();

        if (vm.Type is not (EmployeeTransactionType.Advance or EmployeeTransactionType.Bonus))
            ModelState.AddModelError(nameof(vm.Type), "Invalid type.");
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(x => x.ErrorMessage));
            return RedirectToAction(nameof(Details), new { id = vm.EmployeeId });
        }

        _db.EmployeeTransactions.Add(new EmployeeTransaction
        {
            EmployeeId = e.Id,
            TransactionDate = vm.Date.Date,
            Type = vm.Type,
            NetPaid = vm.Amount!.Value,
            PaymentSource = vm.PaymentSource,
            Remarks = vm.Remarks?.Trim()
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{vm.Type} of Rs. {vm.Amount:N2} given to {e.Name}.";
        return RedirectToAction(nameof(Details), new { id = e.Id });
    }

    // =========================================================
    // Money: salary payout
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> PaySalary(int id)
    {
        var e = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e is null)
            return NotFound();

        var lastTo = await _db.EmployeeTransactions
            .Where(x => x.EmployeeId == id && x.Type == EmployeeTransactionType.SalaryPayment)
            .MaxAsync(x => x.PeriodTo);

        var from = (lastTo?.AddDays(1) ?? e.JoiningDate).Date;
        var to = e.PayType switch
        {
            EmployeePayType.Daily => DateTime.Today,
            EmployeePayType.Weekly => from.AddDays(6),
            _ => new DateTime(from.Year, from.Month, DateTime.DaysInMonth(from.Year, from.Month))
        };
        if (to < from) to = from;

        var outstanding = await OutstandingAdvanceAsync(id);
        var gross = SuggestGross(e.PayType, e.PayRate, from, to);

        return View(new SalaryPaymentFormViewModel
        {
            EmployeeId = e.Id,
            EmployeeName = e.Name,
            PayType = e.PayType,
            PayRate = e.PayRate,
            OutstandingAdvance = outstanding,
            PeriodFrom = from,
            PeriodTo = to,
            GrossAmount = gross,
            AdvanceDeducted = Math.Max(0, Math.Min(outstanding, gross))
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PaySalary(SalaryPaymentFormViewModel vm)
    {
        var e = await _db.Employees.FindAsync(vm.EmployeeId);
        if (e is null || e.IsDeleted)
            return NotFound();

        var outstanding = await OutstandingAdvanceAsync(e.Id);
        var gross = vm.GrossAmount ?? 0;
        var deducted = vm.AdvanceDeducted ?? 0;

        if (vm.PeriodTo < vm.PeriodFrom)
            ModelState.AddModelError(nameof(vm.PeriodTo), "Period end must be on or after the start.");
        if (deducted > gross)
            ModelState.AddModelError(nameof(vm.AdvanceDeducted), "Deduction cannot be more than the gross salary.");
        if (deducted > outstanding)
            ModelState.AddModelError(nameof(vm.AdvanceDeducted), $"Only Rs. {outstanding:N2} advance is outstanding.");

        var overlap = await _db.EmployeeTransactions.AnyAsync(x =>
            x.EmployeeId == e.Id && x.Type == EmployeeTransactionType.SalaryPayment &&
            x.PeriodFrom <= vm.PeriodTo && x.PeriodTo >= vm.PeriodFrom);
        if (overlap)
            ModelState.AddModelError(string.Empty, "Salary has already been paid for part of this period. Check the statement.");

        if (!ModelState.IsValid)
        {
            vm.EmployeeName = e.Name;
            vm.PayType = e.PayType;
            vm.PayRate = e.PayRate;
            vm.OutstandingAdvance = outstanding;
            return View(vm);
        }

        _db.EmployeeTransactions.Add(new EmployeeTransaction
        {
            EmployeeId = e.Id,
            TransactionDate = vm.Date.Date,
            Type = EmployeeTransactionType.SalaryPayment,
            PeriodFrom = vm.PeriodFrom.Date,
            PeriodTo = vm.PeriodTo.Date,
            GrossAmount = gross,
            AdvanceDeducted = deducted,
            NetPaid = gross - deducted,
            PaymentSource = vm.PaymentSource,
            Remarks = vm.Remarks?.Trim()
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Salary paid to {e.Name}: Rs. {gross - deducted:N2} (gross {gross:N2}, advance deducted {deducted:N2}).";
        return RedirectToAction(nameof(Details), new { id = e.Id });
    }

    /// <summary>Removes a wrong entry (soft delete). Admins any time; workers same day only.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTransaction(int id)
    {
        var t = await _db.EmployeeTransactions.FindAsync(id);
        if (t is null || t.IsDeleted)
            return NotFound();

        if (!User.IsInRole(DbSeeder.AdminRole) && t.CreatedDate.Date != DateTime.Today)
        {
            TempData["Error"] = "Only an admin can delete an entry after the day it was made.";
            return RedirectToAction(nameof(Details), new { id = t.EmployeeId });
        }

        // Deleting an advance must not leave more deducted than was ever given.
        if (t.Type == EmployeeTransactionType.Advance)
        {
            var after = await OutstandingAdvanceAsync(t.EmployeeId) - t.NetPaid;
            if (after < 0)
            {
                TempData["Error"] = "This advance has already been (partly) recovered from salary. Delete the salary entry first.";
                return RedirectToAction(nameof(Details), new { id = t.EmployeeId });
            }
        }

        _db.EmployeeTransactions.Remove(t);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{t.Type} entry of Rs. {t.NetPaid:N2} deleted.";
        return RedirectToAction(nameof(Details), new { id = t.EmployeeId });
    }

    // =========================================================
    // All-employees summary
    // =========================================================

    public async Task<IActionResult> Summary(DateTime? from, DateTime? to)
    {
        var (f, t) = RangeMonth(from, to);
        return View(new EmployeeSummaryViewModel { From = f, To = t, Rows = await QuerySummaryAsync(f, t) });
    }

    public async Task<IActionResult> SummaryExcel(DateTime? from, DateTime? to)
    {
        var (f, t) = RangeMonth(from, to);
        var rows = await QuerySummaryAsync(f, t);
        var bytes = _export.ToExcel("Employee Payments Summary", Subtitle(f, t),
            new[] { "Employee", "Designation", "Gross Salary", "Advance Given", "Advance Deducted", "Salary Paid", "Bonus", "Total Paid", "Advance Balance" },
            rows.Select(r => new object?[] { r.Name, r.Designation, r.SalaryGross, r.AdvanceGiven, r.AdvanceDeducted, r.SalaryPaid, r.Bonus, r.TotalPaid, r.AdvanceBalance }));
        return File(bytes, ExcelContentType, $"employee-summary-{f:yyyyMMdd}-{t:yyyyMMdd}.xlsx");
    }

    public async Task<IActionResult> SummaryPdf(DateTime? from, DateTime? to)
    {
        var (f, t) = RangeMonth(from, to);
        var rows = await QuerySummaryAsync(f, t);
        var bytes = _export.ToPdf("Employee Payments Summary", Subtitle(f, t),
            new[] { "Employee", "Designation", "Gross", "Adv. Given", "Adv. Ded.", "Salary Paid", "Bonus", "Total Paid", "Adv. Bal" },
            rows.Select(r => new object?[] { r.Name, r.Designation, r.SalaryGross, r.AdvanceGiven, r.AdvanceDeducted, r.SalaryPaid, r.Bonus, r.TotalPaid, r.AdvanceBalance }));
        return File(bytes, "application/pdf", $"employee-summary-{f:yyyyMMdd}-{t:yyyyMMdd}.pdf");
    }

    private async Task<List<EmployeeSummaryRowViewModel>> QuerySummaryAsync(DateTime from, DateTime to)
    {
        var end = to.AddDays(1);
        var rows = await _db.Employees.AsNoTracking()
            .OrderByDescending(e => e.IsActive).ThenBy(e => e.Name)
            .Select(e => new EmployeeSummaryRowViewModel
            {
                Id = e.Id,
                Name = e.Name,
                Designation = e.Designation,
                IsActive = e.IsActive,
                SalaryGross = e.Transactions.Where(t => !t.IsDeleted && t.TransactionDate >= from && t.TransactionDate < end && t.Type == EmployeeTransactionType.SalaryPayment).Sum(t => (decimal?)t.GrossAmount) ?? 0,
                AdvanceDeducted = e.Transactions.Where(t => !t.IsDeleted && t.TransactionDate >= from && t.TransactionDate < end).Sum(t => (decimal?)t.AdvanceDeducted) ?? 0,
                SalaryPaid = e.Transactions.Where(t => !t.IsDeleted && t.TransactionDate >= from && t.TransactionDate < end && t.Type == EmployeeTransactionType.SalaryPayment).Sum(t => (decimal?)t.NetPaid) ?? 0,
                AdvanceGiven = e.Transactions.Where(t => !t.IsDeleted && t.TransactionDate >= from && t.TransactionDate < end && t.Type == EmployeeTransactionType.Advance).Sum(t => (decimal?)t.NetPaid) ?? 0,
                Bonus = e.Transactions.Where(t => !t.IsDeleted && t.TransactionDate >= from && t.TransactionDate < end && t.Type == EmployeeTransactionType.Bonus).Sum(t => (decimal?)t.NetPaid) ?? 0,
                AdvanceBalance =
                    (e.Transactions.Where(t => !t.IsDeleted && t.Type == EmployeeTransactionType.Advance).Sum(t => (decimal?)t.NetPaid) ?? 0)
                    - (e.Transactions.Where(t => !t.IsDeleted).Sum(t => (decimal?)t.AdvanceDeducted) ?? 0)
            })
            .ToListAsync();

        // Hide inactive employees with nothing to show.
        return rows.Where(r => r.IsActive || r.TotalPaid != 0 || r.AdvanceBalance != 0).ToList();
    }

    // =========================================================
    // Helpers
    // =========================================================

    /// <summary>Suggested gross for a period. Daily = days × rate; Weekly = rate × days/7;
    /// Monthly = rate for each whole calendar month, pro-rated by days otherwise.
    /// Mirrored in Views/Employees/PaySalary.cshtml (JS) — keep both in sync.</summary>
    public static decimal SuggestGross(EmployeePayType type, decimal rate, DateTime from, DateTime to)
    {
        var days = (to.Date - from.Date).Days + 1;
        if (days <= 0) return 0;
        return type switch
        {
            EmployeePayType.Daily => rate * days,
            EmployeePayType.Weekly => Math.Round(rate * days / 7m, 2),
            _ => MonthlyGross(rate, from.Date, to.Date)
        };
    }

    private static decimal MonthlyGross(decimal rate, DateTime from, DateTime to)
    {
        decimal total = 0;
        var cursor = from;
        while (cursor <= to)
        {
            var monthEnd = new DateTime(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month));
            var segEnd = monthEnd < to ? monthEnd : to;
            var segDays = (segEnd - cursor).Days + 1;
            total += rate * segDays / DateTime.DaysInMonth(cursor.Year, cursor.Month);
            cursor = segEnd.AddDays(1);
        }
        return Math.Round(total, 2);
    }

    private async Task<decimal> OutstandingAdvanceAsync(int employeeId)
    {
        var all = _db.EmployeeTransactions.Where(x => x.EmployeeId == employeeId);
        return (await all.Where(x => x.Type == EmployeeTransactionType.Advance).SumAsync(x => (decimal?)x.NetPaid) ?? 0)
               - (await all.SumAsync(x => (decimal?)x.AdvanceDeducted) ?? 0);
    }

    [GeneratedRegex(@"^\d{13}$")]
    private static partial Regex ThirteenDigits();

    /// <summary>Normalises CNIC to 12345-1234567-1 and checks uniqueness. Returns the
    /// normalised value (or the original on error so the form redisplays it).</summary>
    private async Task<string?> ValidateCnicAsync(string? cnic, int? currentId)
    {
        if (string.IsNullOrWhiteSpace(cnic))
            return null;

        var digits = new string(cnic.Where(char.IsDigit).ToArray());
        if (!ThirteenDigits().IsMatch(digits))
        {
            ModelState.AddModelError(nameof(EmployeeFormViewModel.Cnic), "CNIC must have 13 digits (e.g. 37405-1234567-1).");
            return cnic;
        }

        var formatted = $"{digits[..5]}-{digits[5..12]}-{digits[12]}";
        var taken = await _db.Employees.AnyAsync(e => e.Cnic == formatted && e.Id != currentId);
        if (taken)
            ModelState.AddModelError(nameof(EmployeeFormViewModel.Cnic), "Another employee already has this CNIC.");
        return formatted;
    }

    private async Task<(string? Photo, string? Front, string? Back)> SaveFilesAsync(EmployeeFormViewModel vm)
    {
        if (!ModelState.IsValid)
            return (null, null, null);

        var photo = await _files.SaveAsync(vm.Photo, UploadFolder);
        if (photo.Error is not null) ModelState.AddModelError(nameof(vm.Photo), photo.Error);
        var front = await _files.SaveAsync(vm.CnicFront, UploadFolder);
        if (front.Error is not null) ModelState.AddModelError(nameof(vm.CnicFront), front.Error);
        var back = await _files.SaveAsync(vm.CnicBack, UploadFolder);
        if (back.Error is not null) ModelState.AddModelError(nameof(vm.CnicBack), back.Error);
        return (photo.Path, front.Path, back.Path);
    }

    private static void Apply(Employee e, EmployeeFormViewModel vm, (string? Photo, string? Front, string? Back) files)
    {
        e.Name = vm.Name.Trim();
        e.FatherName = vm.FatherName?.Trim();
        e.Phone = vm.Phone?.Trim();
        e.Address = vm.Address?.Trim();
        e.Cnic = vm.Cnic;
        e.Designation = vm.Designation?.Trim();
        e.JoiningDate = vm.JoiningDate.Date;
        e.PayType = vm.PayType;
        e.PayRate = vm.PayRate ?? 0;
        e.IsActive = vm.IsActive;
        e.Remarks = vm.Remarks?.Trim();
        if (files.Photo is not null) e.PhotoPath = files.Photo;
        if (files.Front is not null) e.CnicFrontPath = files.Front;
        if (files.Back is not null) e.CnicBackPath = files.Back;
    }

    private static (DateTime from, DateTime to) Range(DateTime? from, DateTime? to)
    {
        var f = (from ?? DateTime.Today).Date;
        var t = (to ?? f).Date;
        return t < f ? (t, f) : (f, t);
    }

    /// <summary>Defaults to the current calendar month.</summary>
    private static (DateTime from, DateTime to) RangeMonth(DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var f = (from ?? new DateTime(today.Year, today.Month, 1)).Date;
        var t = (to ?? today).Date;
        return t < f ? (t, f) : (f, t);
    }

    private static string Subtitle(DateTime from, DateTime to) =>
        from == to ? $"{from:dd MMM yyyy}" : $"{from:dd MMM yyyy} — {to:dd MMM yyyy}";
}
