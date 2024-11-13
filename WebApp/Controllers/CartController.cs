using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using WebApp.Services;

namespace WebApp.Models;

[Authorize]
public class CartController : Controller
{
    OrganicContext context;
    VnPaymentService service;
    public CartController(OrganicContext context, VnPaymentService service)
    {
        this.context = context;
        this.service = service;
    }

    public IActionResult Checkout()
    {
        ViewBag.Departments = context.Departments.ToList();
        ViewBag.Categories = context.Categories.ToList();
        string? memberId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(memberId))
        {
            return Redirect("/auth/login");
        }
        List<Cart> carts = context.Carts.Include(p => p.Product).Where(p => p.MemberId == memberId).ToList();
        ViewBag.Total = carts.Sum(p => p.Quantity * p.Product?.Price);
        ViewBag.Carts = carts;
        return View(new Invoice
        {
            GivenName = User.FindFirstValue(ClaimTypes.GivenName)!,
            Surname = User.FindFirstValue(ClaimTypes.Surname)
        });
    }
    public IActionResult VnPaymentResponse(VnPayment obj)
    {
        if (ModelState.IsValid)
        {
            context.VnPayments.Add(obj);  // Thêm bản ghi vào DbSet VnPayments
            context.SaveChanges();  // Lưu vào cơ sở dữ liệu
            return Json(new { success = true, message = "Lưu thông tin thanh toán thành công." });
        }
        return Json(new { success = false, message = "Thông tin không hợp lệ." });
    }


    [HttpPost]
    public IActionResult Checkout(Invoice obj)
    {
        string? memberId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(memberId))
        {
            return Redirect("/auth/login");
        }
        Random random = new Random();
        obj.InvoiceId = random.Next(999999, 99999999);
        obj.InvoiceDate = DateTime.Now;
        obj.MemberId = memberId;
        List<Cart> carts = context.Carts.Include(p => p.Product).Where(p => p.MemberId == memberId).ToList();

        obj.Amount = carts.Sum(p => p.Quantity * p.Product!.Price) * 1000;

        obj.InvoiceDetails = new List<InvoiceDetail>(carts.Count);
        foreach (var item in carts)
        {
            obj.InvoiceDetails.Add(new InvoiceDetail
            {
                InvoiceId = obj.InvoiceId,
                ProductId = item.ProductId,
                Price = item.Product!.Price,
                Quantity = item.Quantity
            });
        }
        context.Invoices.Add(obj);
        int ret = context.SaveChanges();
        if (ret > 0)
        {
            string url = service.ToUrl(obj, HttpContext);
            System.Console.WriteLine("********************");
            System.Console.WriteLine(url);
            return Redirect(url);
            //return Redirect("/cart/success");
        }
        ViewBag.Departments = context.Departments.ToList();
        ViewBag.Categories = context.Categories.ToList();
        ViewBag.Total = carts.Sum(p => p.Quantity * p.Product?.Price);
        ViewBag.Carts = carts;
        return View(obj);
    }
    [HttpPost]
    public IActionResult Add(Cart obj)
    {
        string? memberId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(memberId))
        {
            return Redirect("/auth/login");
        }
        obj.MemberId = memberId;

        if (context.Carts.Any(p => p.MemberId == obj.MemberId && p.ProductId == obj.ProductId))
        {
            Cart? cart = context.Carts.FirstOrDefault(p => p.MemberId == obj.MemberId && p.ProductId == obj.ProductId);
            if (cart != null)
            {
                cart.Quantity += obj.Quantity;
                cart.UpdatedDate = DateTime.Now;
                context.Carts.Update(cart);
            }
        }
        else
        {
            obj.CreatedDate = DateTime.Now;
            obj.UpdatedDate = DateTime.Now;
            context.Carts.Add(obj);
        }
        context.SaveChanges();
        return Redirect("/cart");
    }
    public IActionResult Index()
    {
        //Miss
        ViewBag.Departments = context.Departments.ToList();

        ViewBag.Categories = context.Categories.ToList();

        string? memberId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(memberId))
        {
            return Redirect("/auth/login");
        }
        return View(context.Carts.Include(p => p.Product).Where(p => p.MemberId == memberId).ToList());
    }
    //Tự làm

    [HttpPost]
    public IActionResult Delete(int id)
    {
        {
            string? memberId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(memberId))
            {
                return Redirect("/auth/login");
            }

            var cartItem = context.Carts.FirstOrDefault(p => p.CartId == id && p.MemberId == memberId);
            if (cartItem != null)
            {
                context.Carts.Remove(cartItem);
                context.SaveChanges();
            }

            return Redirect("/cart");
        }
    }
    [HttpPost]
    public IActionResult Edit(Dictionary<int, Cart> CartItems)
    {
        string? memberId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(memberId))
        {
            return Redirect("/auth/login");
        }

        foreach (var item in CartItems)
        {
            int cartId = item.Key;
            short quantity = item.Value.Quantity;

            // Find the cart item by CartId and MemberId
            var cartItem = context.Carts.FirstOrDefault(c => c.CartId == cartId && c.MemberId == memberId);
            if (cartItem != null)
            {
                // Update the quantity and timestamps
                cartItem.Quantity = quantity;
                cartItem.UpdatedDate = DateTime.Now;
            }
        }

        context.SaveChanges(); // Persist changes to the database
        return Redirect("/cart");
    }
}