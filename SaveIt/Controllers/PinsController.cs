﻿using Humanizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NuGet.Protocol.Plugins;
using SaveIt.Data;
using SaveIt.Models;

namespace SaveIt.Controllers
{
    public class PinsController : Controller
    {
        private readonly ApplicationDbContext db;

        private readonly UserManager<ApplicationUser> _userManager;

        private readonly RoleManager<IdentityRole> _roleManager;

        public PinsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            db = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        //[Authorize(Roles = "User,Admin")]
        public IActionResult Index()
        {
            //var pins = db.Pins.Include("PinTags.Tag");
            /*var pins = db.Pins.Include("User").Include(p => p.PinTags).ThenInclude(pt => pt.Tag).Include(p => p.Likes);
            ViewBag.Pins = pins;
            
            if (TempData.ContainsKey("message"))
            {
                ViewBag.Message = TempData["message"];
                ViewBag.Alert = TempData["messageType"];
            }
            return View();*/

            int perPage = 3;

            var pins = db.Pins.Include("User").Include(p => p.PinTags).ThenInclude(pt => pt.Tag).Include(p => p.Likes).OrderByDescending(p => p.Likes.Count).ThenByDescending(p => p.Date);

            var search = "";

            if (Convert.ToString(HttpContext.Request.Query["search"]) != null)
            {
                search = Convert.ToString(HttpContext.Request.Query["search"]).Trim();
                List<int> pinIds = db.Pins.Where(p => p.Title.Contains(search) || p.Content.Contains(search)).Select(p => p.Id).ToList();
                List<int> pinIdsOfTags = db.Tags.Where(t => t.TagName.Contains(search)).SelectMany(t => t.PinTags.Select(pt => pt.PinId)).Distinct().ToList();
                List<int?> pinIdsOfBoards = db.Boards.Where(b => b.Name.Contains(search)).SelectMany(b => b.PinBoards.Select(pb => pb.PinId)).Distinct().ToList();
                foreach (var pinIdOfBoard in pinIdsOfBoards)
                {
                    if (pinIdOfBoard != null)
                    {
                        pinIds.Add((int)pinIdOfBoard);
                    }
                }

                List<int> mergedIds = pinIds.Union(pinIdsOfTags).ToList();
                
                pins = db.Pins.Where(p => mergedIds.Contains(p.Id)).Include("User").Include(p => p.PinTags).ThenInclude(pt => pt.Tag).Include(p => p.Likes).OrderBy(p => p.Title).ThenByDescending(p => p.Likes.Count).ThenByDescending(p => p.Date);
            }

            ViewBag.SearchString = search;




            if (TempData.ContainsKey("message"))
            {
                ViewBag.Message = TempData["message"];
                ViewBag.Alert = TempData["messageType"];
            }

            var totalItems = pins.Count();

            var currentPage = Convert.ToInt32(HttpContext.Request.Query["page"]);

            var offset = 0;

            if (!currentPage.Equals(0))
            {
                offset = (currentPage - 1) * perPage;
            }

            var paginatedPins = pins.Skip(offset).Take(perPage);

            ViewBag.LastPage = Math.Ceiling((float)totalItems / (float)perPage);

            ViewBag.Pins = paginatedPins;


            ViewBag.PaginationBaseUrl = "";

            if (search != "")
            {
                ViewBag.PaginationBaseUrl = "/Pins/Index/?search=" + search + "&page";
            }
            else
            {
                ViewBag.PaginationBaseUrl = "/Pins/Index/?page";
            }


            return View();
        }

        [Authorize(Roles = "User,Admin")]
        public IActionResult Show(int id)
        {

            //Pin pin = db.Pins.Include("PinTags.Tag").Include("Comments").Where(p => p.Id == id).First();
            Pin pin = db.Pins.Include("User").Include(p => p.PinTags).ThenInclude(pt => pt.Tag).Include(p => p.Likes).Include(p => p.Comments).Include("Comments.User").FirstOrDefault(p => p.Id == id);
            var usr = _userManager.GetUserId(User);
            var likes = db.Likes.Where(l => l.PinId == id && usr == l.UserId).ToList();
            //ViewBag.Path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot\\images");
            ViewBag.UserBoards = db.Boards.Where(b => b.UserId == _userManager.GetUserId(User)).ToList();
            if (likes.Count > 0)
            {
                ViewBag.Liked = true;
            }
            else
            {
                ViewBag.Liked = false;
            }
            SetAccessRights();
            return View(pin);
        }

        private void SetAccessRights()
        {
            ViewBag.EsteAdmin = User.IsInRole("Admin");
            ViewBag.UserCurent = _userManager.GetUserId(User);
        }

        [Authorize(Roles = "User,Admin")]
        [HttpPost]
        public IActionResult Show([FromForm] Comment comment)
        {
            comment.Date = System.DateTime.Now;
            comment.UserId = _userManager.GetUserId(User);
            if (ModelState.IsValid)
            {
                db.Comments.Add(comment);
                db.SaveChanges();
                return Redirect("/Pins/Show/" + comment.PinId);
            }
            else
            {
                //Pin pin = db.Pins.Include("PinTags.Tag").Include("Comments").Where(p => p.Id == comment.PinId).First();
                Pin pin = db.Pins.Include(p => p.PinTags).ThenInclude(pt => pt.Tag).Include(p => p.Likes).Include(p => p.Comments).Include("Comments.User").FirstOrDefault(p => p.Id == comment.PinId);
                ViewBag.UserBoards = db.Boards.Where(b => b.UserId == _userManager.GetUserId(User)).ToList();
                SetAccessRights();
                return View(pin);
            }
        }

        [HttpPost]
        public IActionResult AddBoard([FromForm] PinBoard pinBoard)
        {
            if (ModelState.IsValid)
            {
                
                if (db.PinBoards.Where(pb => pb.BoardId == pinBoard.BoardId && pb.PinId == pinBoard.PinId).Count() > 0)
                {
                    TempData["message"] = "Pin-ul este deja in board!";
                    TempData["messageType"] = "alert-danger";
                }
                else
                {
                    db.PinBoards.Add(pinBoard);
                    db.SaveChanges();
                    TempData["message"] = "Pin-ul a fost adaugat in board!";
                    TempData["messageType"] = "alert-success";
                }

            }
            else
            {
                TempData["message"] = "Nu s-a putut adauga in Board!";
                TempData["messageType"] = "alert-danger";
            }
            return Redirect("/Pins/Show/" + pinBoard.PinId);
        }

        [NonAction]
        public IEnumerable<SelectListItem> GetAllTags()
        {
            var selectList = new List<SelectListItem>();
            var tags = from t in db.Tags select t;

            foreach (var tag in tags)
            {
                selectList.Add(new SelectListItem
                {
                    Value = tag.Id.ToString(),
                    Text = tag.TagName.ToString()
                });
            }
            return selectList;
        }

        [Authorize(Roles = "User,Admin")]
        public IActionResult New()
        {
            Pin pin = new Pin();
            pin.Tags = GetAllTags();
            pin.mediaPath = null;
            return View(pin);
        }

        [Authorize(Roles = "User,Admin")]
        [HttpPost]
        public async Task<IActionResult> New(Pin pin, IFormFile? PinPhoto)
        {
            if (PinPhoto != null)
            {
                if (pin.mediaPath != null)
                {
                    pin.Tags = GetAllTags();
                    ModelState.AddModelError("mediaPath", "Nu puteti adauga o imagine si un link " +
                        "pentru video in acelasi timp!");
                    return View(pin);
                }
                var fileName = Path.GetFileName(PinPhoto.FileName);
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot\\images", fileName);
                using (var fileSteam = new FileStream(filePath, FileMode.Create))
                {
                    await PinPhoto.CopyToAsync(fileSteam);
                }
                pin.mediaType = "image";
                pin.mediaPath = fileName;
            }
            else if (pin.mediaPath != null)
            {
                pin.mediaType = "video";
            }
            else
            {
                pin.Tags = GetAllTags();
                ModelState.AddModelError("mediaPath", "Trebuie sa adaugati o imagine sau un link pentru video!");
                return View(pin);
            }

            pin.Date = DateTime.Now;
            pin.UserId = _userManager.GetUserId(User);

            /*foreach (var state in ModelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    Console.WriteLine($"Atribut: {state.Key}, Eroare: {error.ErrorMessage}");
                }
            }*/

            if (ModelState.IsValid)
            {
                db.Pins.Add(pin);
                db.SaveChanges();
                foreach (var tagId in pin.TagIds)
                {
                    PinTag pinTag = new PinTag
                    {
                        PinId = pin.Id,
                        TagId = tagId
                    };
                    db.PinTags.Add(pinTag);
                }
                db.SaveChanges();
                TempData["message"] = "Pin-ul a fost adaugat!";
                TempData["messageType"] = "alert-success";
                return RedirectToAction("Index");
            }
            else
            {
                pin.Tags = GetAllTags();
                return View(pin);
            }
        }

        [Authorize(Roles = "User,Admin")]
        public IActionResult Edit(int id)
        {
            Pin pin = db.Pins.Include("User").Include(p => p.PinTags).ThenInclude(pt => pt.Tag).FirstOrDefault(p => p.Id == id);
            pin.Tags = GetAllTags();
            pin.TagIds = pin.PinTags.Select(pt => pt.TagId).ToList();

            if (pin.UserId == _userManager.GetUserId(User) || User.IsInRole("Admin"))
            {
                return View(pin);
            }
            else
            {
                TempData["message"] = "Nu aveti drepturi pentru aceasta actiune!";
                TempData["messageType"] = "alert-danger";
                return RedirectToAction("Index");
            }

        }

        [Authorize(Roles = "User,Admin")]
        [HttpPost]
        public async Task<IActionResult> Edit(int id, Pin requestPin, IFormFile? PinPhoto)
        {
            if (ModelState.IsValid)
            {
                Pin pin = db.Pins.Include("User").Include(p => p.PinTags).ThenInclude(pt => pt.Tag).FirstOrDefault(p => p.Id == id);
                
                if (pin.UserId != _userManager.GetUserId(User) && !User.IsInRole("Admin"))
                {
                    TempData["message"] = "Nu aveti drepturi pentru aceasta actiune!";
                    TempData["messageType"] = "alert-danger";
                    return RedirectToAction("Index");
                }

                pin.PinTags.Clear();

                foreach (var tagId in requestPin.TagIds)
                {
                    PinTag pinTag = new PinTag
                    {
                        PinId = pin.Id,
                        TagId = tagId
                    };
                    db.PinTags.Add(pinTag);
                    pin.PinTags.Add(pinTag);
                }
                if (PinPhoto != null)
                {
                    if (pin.mediaPath != requestPin.mediaPath)
                    {
                        pin.Tags = GetAllTags();
                        ModelState.AddModelError("mediaPath", "Nu puteti adauga o imagine si un link " +
                                                       "pentru video in acelasi timp!");
                        return View(pin);
                    }
                    var fileName = Path.GetFileName(PinPhoto.FileName);
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot\\images", fileName);
                    using (var fileSteam = new FileStream(filePath, FileMode.Create))
                    {
                        await PinPhoto.CopyToAsync(fileSteam);
                    }
                    pin.mediaType = "image";
                    pin.mediaPath = fileName;
                }
                else if (requestPin.mediaPath != null)
                {
                    pin.mediaType = "video";
                    pin.mediaPath = requestPin.mediaPath;
                }
                else
                {
                    pin.Tags = GetAllTags();
                    ModelState.AddModelError("mediaPath", "Trebuie sa adaugati o imagine sau un link pentru video!");
                    return View(pin);
                }

                pin.Title = requestPin.Title;
                pin.Content = requestPin.Content;
                pin.Date = DateTime.Now;
                pin.TagIds = requestPin.TagIds.ToList();
                pin.Tags = GetAllTags();

                db.SaveChanges();
                TempData["message"] = "Pin-ul a fost modificat!";
                TempData["messageType"] = "alert-success";
                return RedirectToAction("Index");
            }
            else
            {
                requestPin.Tags = GetAllTags();
                return View(requestPin);
            }
        }
        [Authorize(Roles = "User,Admin")]
        [HttpPost]
        public IActionResult Delete(int id)
        {
            var pin = db.Pins.Include("Comments").Where(p => p.Id == id).First();
            if (pin.UserId != _userManager.GetUserId(User) && !User.IsInRole("Admin"))
            {
                TempData["message"] = "Nu aveti drepturi pentru aceasta actiune!";
                TempData["messageType"] = "alert-danger";
                return RedirectToAction("Index");
            }
            db.Pins.Remove(pin);
            db.SaveChanges();
            TempData["message"] = "Pin-ul a fost sters!";
            return RedirectToAction("Index");
        }

    }
}
