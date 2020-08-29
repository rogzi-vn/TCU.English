﻿using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using TCU.English.Models;
using TCU.English.Models.DataManager;
using TCU.English.Models.Repository;
using TCU.English.Utils;

namespace TCU.English.Controllers
{
    [AuthorizeRoles(UserType.ROLE_ALL, UserType.ROLE_MANAGER_LIBRARY)]
    public class ListeningManagerController : Controller
    {
        private readonly IHostEnvironment host;
        private readonly string PATH_ROOT;

        private readonly UserManager _UserManager;
        private readonly UserTypeManager _UserTypeManager;
        private readonly TestCategoryManager _TestCategoryManager;
        private readonly ListeningMediaManager _ListeningMediaManager;
        private readonly ListeningBaseQuestionManager _ListeningBaseQuestionManager;

        public ListeningManagerController(
           IHostEnvironment _host,
           IDataRepository<User> _UserManager,
           IDataRepository<UserType> _UserTypeManager,
           IDataRepository<TestCategory> _TestCategoryManager,
           IDataRepository<ListeningMedia> _ListeningMediaManager,
           IDataRepository<ListeningBaseQuestion> _ListeningBaseQuestionManager)
        {
            host = _host;
            PATH_ROOT = Path.GetDirectoryName(host.ContentRootPath);

            this._UserManager = (UserManager)_UserManager;
            this._UserTypeManager = (UserTypeManager)_UserTypeManager;
            this._TestCategoryManager = (TestCategoryManager)_TestCategoryManager;
            this._ListeningMediaManager = (ListeningMediaManager)_ListeningMediaManager;
            this._ListeningBaseQuestionManager = (ListeningBaseQuestionManager)_ListeningBaseQuestionManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        #region PART 1
        public IActionResult Part1(
           int categoryPage = 1,
           string categorySearchKey = "")
        {
            int limit = 20;
            int categoryStart = (categoryPage - 1) * limit;

            var testCategories = _TestCategoryManager.GetByPagination(TestCategory.LISTENING, 1, categoryStart, limit);

            // Tạo đối tượng phân trang cho Category
            ViewBag.CategoryPagination = new Pagination(nameof(Part1), NameUtils.ControllerName<ListeningManagerController>())
            {
                PageKey = nameof(categoryPage),
                PageCurrent = categoryPage,
                NumberPage = PaginationUtils.TotalPageCount(testCategories.Count(), limit),
                Offset = limit
            };

            return View($"{nameof(Part1)}/Index", testCategories);
        }

        [HttpGet]
        public IActionResult Part1Create()
        {
            return View($"{nameof(Part1)}/{nameof(Part1Create)}", new ListeningBaseCombined
            {
                TestCategory = TestCategory.ListeningCategory(1),
                ListeningMedia = ListeningMedia.Generate(),
                ListeningBaseQuestions = ListeningBaseQuestion.Generate(1)
            });
        }

        [HttpPost]
        public async Task<IActionResult> Part1Create(ListeningBaseCombined listeningBaseCombined, IFormFile audio, params IFormFile[] images)
        {
            ModelState.Remove(nameof(ListeningBaseQuestion.Answers));
            if (listeningBaseCombined != null && listeningBaseCombined.TestCategory != null &&
                listeningBaseCombined.TestCategory != null &&
                listeningBaseCombined.TestCategory.Name != null && listeningBaseCombined.TestCategory.Name.Length > 0 &&
                listeningBaseCombined.TestCategory.TypeCode != null && listeningBaseCombined.TestCategory.TypeCode.Length > 0 &&
                listeningBaseCombined.TestCategory.PartId > 0 &&
                listeningBaseCombined.ListeningMedia != null &&
                listeningBaseCombined.ListeningBaseQuestions != null &&
                listeningBaseCombined.ListeningBaseQuestions.Count > 0 &&
                audio != null && audio.Length > 0)
            {
                // Tổng số câu trả lời
                int sumOfAnswer = 0;
                // Lấy mã người tạo
                int userId = User.Id();
                // Cập nhật mã người tạo cho category
                listeningBaseCombined.TestCategory.CreatorId = userId;
                // Tiến hành kiểm tra trong các phần câu hỏi và đặt mã người tạo + convert đối tượng thành Json và gán vào ANSWERS
                for (int i = 0; i < listeningBaseCombined.ListeningBaseQuestions.Count; i++)
                {
                    if (listeningBaseCombined.ListeningBaseQuestions[i].QuestionText == null || listeningBaseCombined.ListeningBaseQuestions[i].QuestionText.Length <= 0)
                    {
                        ModelState.AddModelError(string.Empty, $"{nameof(ListeningBaseQuestion.QuestionText)} of question {i + 1} is required.");
                        return View($"{nameof(Part1)}/{nameof(Part1Create)}", listeningBaseCombined);
                    }
                    if (listeningBaseCombined.ListeningBaseQuestions[i].AnswerList.Count <= 0 || listeningBaseCombined.ListeningBaseQuestions[i].AnswerList.Any(x => string.IsNullOrEmpty(x.AnswerContent)))
                    {
                        ModelState.AddModelError(string.Empty, $"{nameof(ListeningBaseQuestion.Answers)} of question {i + 1} is required.");
                        return View($"{nameof(Part1)}/{nameof(Part1Create)}", listeningBaseCombined);
                    }
                    if (!listeningBaseCombined.ListeningBaseQuestions[i].AnswerList.Any(it => it.IsCorrect))
                    {
                        ModelState.AddModelError(string.Empty, $"{nameof(ListeningBaseQuestion.Answers)} of question {i + 1} must have correct option.");
                        return View($"{nameof(Part1)}/{nameof(Part1Create)}", listeningBaseCombined);
                    }
                    listeningBaseCombined.ListeningBaseQuestions[i].CreatorId = userId;
                    sumOfAnswer += listeningBaseCombined.ListeningBaseQuestions[i].AnswerList.Count;
                }
                // Nếu số ảnh bé hơn số câu trả lời, 
                // tức là GV chưa cung cấp đủ hình ảnh cho các câu trả lời, 
                // cho nên cần phải yêu cầu họ cung cấp lại cho đủ.
                if (sumOfAnswer > images.Length)
                {
                    ModelState.AddModelError(string.Empty, "Please provide full photos for the answers.");
                }
                else
                {
                    // Nếu đã đủ thì tiến hành các bước tiếp theo
                    //-------------------------------------------------//
                    // Tiến hành thêm danh mục vào CSDL và lấy ID
                    _TestCategoryManager.Add(listeningBaseCombined.TestCategory);
                    //
                    if (listeningBaseCombined.TestCategory.Id > 0)
                    {
                        // Tiến hành tải audio lên
                        string audioUploadPath = await host.UploadForTestMedia(audio, TestCategory.LISTENING, 1);
                        if (audioUploadPath == null || audioUploadPath.Length <= 0)
                        {
                            // Nếu gặp sự cố thì tiến hành xóa bỏ mục câu hỏi và trở lại trang thêm để thông báo
                            _TestCategoryManager.Delete(listeningBaseCombined.TestCategory);
                            ModelState.AddModelError(string.Empty, "Cannot upload audio file.");
                            return View($"{nameof(Part1)}/{nameof(Part1Create)}", listeningBaseCombined);
                        }
                        else
                        {
                            // Cập nhật đường dẫn vào
                            listeningBaseCombined.ListeningMedia.Audio = audioUploadPath;
                            // Cập nhật mục nó thuộc về
                            listeningBaseCombined.ListeningMedia.TestCategoryId = listeningBaseCombined.TestCategory.Id;
                            listeningBaseCombined.ListeningMedia.Active = true;
                            // Cập nhật vào CSDl
                            _ListeningMediaManager.Add(listeningBaseCombined.ListeningMedia);
                        }
                        // Tiến hành tải các ảnh lên
                        List<string> uploadImgePaths = new List<string>();
                        for (int i = 0; i < images.Length; i++)
                        {
                            string uploadResult = await host.UploadForTestMedia(images[i], TestCategory.LISTENING, 1);
                            if (uploadResult == null || uploadResult.Length <= 0)
                            {
                                uploadResult = "";
                            }
                            uploadImgePaths.Add(uploadResult);
                        }
                        // Cập nhật các đường dẫn được tải lên vào các câu trả lời
                        for (int i = 0; i < listeningBaseCombined.ListeningBaseQuestions.Count; i++)
                        {
                            for (int j = 0; j < listeningBaseCombined.ListeningBaseQuestions[i].AnswerList.Count; j++)
                            {
                                listeningBaseCombined.ListeningBaseQuestions[i].AnswerList[j].AnswerContent = uploadImgePaths[i * listeningBaseCombined.ListeningBaseQuestions.Count + j];
                            }
                            listeningBaseCombined.ListeningBaseQuestions[i].Answers = JsonConvert.SerializeObject(listeningBaseCombined.ListeningBaseQuestions[i].AnswerList);
                        }
                        // Cập nhật người tạo, đồng thời thêm vào CSDL
                        for (int i = 0; i < listeningBaseCombined.ListeningBaseQuestions.Count; i++)
                        {
                            listeningBaseCombined.ListeningBaseQuestions[i].TestCategoryId = listeningBaseCombined.TestCategory.Id;
                            _ListeningBaseQuestionManager.Add(listeningBaseCombined.ListeningBaseQuestions[i]);
                        }
                        // Chuyển hướng đến hiển thị danh sách
                        return RedirectToAction(nameof(Part1));
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "An error occurred during execution.");
                    }
                }
            }
            return View($"{nameof(Part1)}/{nameof(Part1Create)}", listeningBaseCombined);
        }

        [HttpGet]
        public IActionResult Part1Update(long id)
        {
            if (id <= 0)
            {
                return BadRequest();
            }
            else
            {
                var testCategory = _TestCategoryManager.Get(id);
                if (testCategory == null)
                {
                    return NotFound();
                }
                var readingPartTwos = _ListeningBaseQuestionManager.GetAll(testCategory.Id).ToList();
                if (readingPartTwos.Count <= 0)
                {
                    readingPartTwos = ListeningBaseQuestion.Generate(10);
                }
                for (int i = 0; i < readingPartTwos.Count(); i++)
                {
                    if (readingPartTwos[i].Answers != null && readingPartTwos[i].Answers.Length > 0)
                    {
                        try
                        {
                            readingPartTwos[i].AnswerList = JsonConvert.DeserializeObject<List<BaseAnswer>>(readingPartTwos[i].Answers);
                        }
                        catch (Exception)
                        {

                        }
                    }
                }
                return View($"{nameof(Part1)}/{nameof(Part1Update)}",
                    new ListeningBaseCombined
                    {
                        TestCategory = testCategory,
                        ListeningBaseQuestions = readingPartTwos
                    });
            }
        }

        [HttpPost]
        public IActionResult Part1Update(ListeningBaseCombined listeningBaseCombined)
        {
            ModelState.Remove(nameof(ListeningBaseQuestion.Answers));
            if (listeningBaseCombined != null && listeningBaseCombined.TestCategory != null &&
                listeningBaseCombined.TestCategory.Name != null && listeningBaseCombined.TestCategory.Name.Length > 0 &&
                listeningBaseCombined.TestCategory.TypeCode != null && listeningBaseCombined.TestCategory.TypeCode.Length > 0 &&
                listeningBaseCombined.TestCategory.PartId > 0 &&
                listeningBaseCombined.TestCategory.WYSIWYGContent != null && listeningBaseCombined.TestCategory.WYSIWYGContent.Length > 0 &&
                listeningBaseCombined.ListeningBaseQuestions.Count > 0)
            {
                // Lấy mã người tạo
                int userId = User.Id();
                // Cập nhật mã người tạo cho category
                if (listeningBaseCombined.TestCategory.CreatorId <= 0)
                    listeningBaseCombined.TestCategory.CreatorId = userId;
                // Tiến hành kiểm tra trong các phần câu hỏi và đặt mã người tạo + convert đối tượng thành Json và gán vào ANSWERS
                for (int i = 0; i < listeningBaseCombined.ListeningBaseQuestions.Count; i++)
                {
                    //if (listeningBaseCombined.ListeningBaseQuestions[i].QuestionText == null || listeningBaseCombined.ListeningBaseQuestions[i].QuestionText.Length <= 0)
                    //{
                    //    ModelState.AddModelError(string.Empty, $"{nameof(ListeningBaseQuestion.QuestionText)} of question {i + 1} is required.");
                    //    return View($"{nameof(Part1)}/{nameof(Part1Update)}", listeningBaseCombined);
                    //}
                    if (listeningBaseCombined.ListeningBaseQuestions[i].AnswerList.Count <= 0 || listeningBaseCombined.ListeningBaseQuestions[i].AnswerList.Any(x => string.IsNullOrEmpty(x.AnswerContent)))
                    {
                        ModelState.AddModelError(string.Empty, $"{nameof(ListeningBaseQuestion.Answers)} of question {i + 1} is required.");
                        return View($"{nameof(Part1)}/{nameof(Part1Update)}", listeningBaseCombined);
                    }
                    if (!listeningBaseCombined.ListeningBaseQuestions[i].AnswerList.Any(it => it.IsCorrect))
                    {
                        ModelState.AddModelError(string.Empty, $"{nameof(ListeningBaseQuestion.Answers)} of question {i + 1} must have correct option.");
                        return View($"{nameof(Part1)}/{nameof(Part1Update)}", listeningBaseCombined);
                    }

                    if (listeningBaseCombined.ListeningBaseQuestions[i].CreatorId <= 0)
                        listeningBaseCombined.ListeningBaseQuestions[i].CreatorId = userId;
                    string json = JsonConvert.SerializeObject(listeningBaseCombined.ListeningBaseQuestions[i].AnswerList);
                    if (listeningBaseCombined.ListeningBaseQuestions[i].Answers != null && listeningBaseCombined.ListeningBaseQuestions[i].Answers != json)
                        listeningBaseCombined.ListeningBaseQuestions[i].Answers = json;
                }
                // Tiến hành thêm danh mục vào CSDL và lấy ID
                _TestCategoryManager.Add(listeningBaseCombined.TestCategory);
                if (listeningBaseCombined.TestCategory.Id > 0)
                {
                    for (int i = 0; i < listeningBaseCombined.ListeningBaseQuestions.Count; i++)
                    {
                        listeningBaseCombined.ListeningBaseQuestions[i].TestCategoryId = listeningBaseCombined.TestCategory.Id;
                        _ListeningBaseQuestionManager.Add(listeningBaseCombined.ListeningBaseQuestions[i]);
                    }
                    return RedirectToAction(nameof(Part1));
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "An error occurred during execution.");
                }
            }
            return View($"{nameof(Part1)}/{nameof(Part1Update)}", listeningBaseCombined);
        }


        [HttpDelete]
        public IActionResult Part1DeleteAjax(long id) // CategoryId
        {
            var category = _TestCategoryManager.Get(id);
            if (category.TypeCode != TestCategory.LISTENING && category.PartId != 4)
            {
                return Json(new { success = false, responseText = "You cannot perform deletion to item other than the current item." });
            }
            if (category == null)
            {
                return Json(new { success = false, responseText = "This test category was not found." });
            }
            else
            {
                _TestCategoryManager.Delete(category);
                return Json(new { success = true, category = JsonConvert.SerializeObject(category), responseText = "Deleted" });
            }
        }

        #endregion
    }
}
