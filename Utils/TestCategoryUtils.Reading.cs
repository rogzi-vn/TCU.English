﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TCU.English.Models;
using TCU.English.Models.DataManager;
using TCU.English.Models.PiceOfTest;

namespace TCU.English.Utils
{
    public static partial class TestCategoryUtils
    {
        public static ReadingTestPaper GenerateReadingTestPaper(this TestCategoryManager _TestCategoryManager)
        {
            return new ReadingTestPaper
            {
                ReadingPartOnes = ReadingTestPaper.GeneratePart1(_TestCategoryManager),
                ReadingPartTwos = ReadingTestPaper.GeneratePart2(_TestCategoryManager),
                ReadingPartThrees = ReadingTestPaper.GeneratePart3(_TestCategoryManager),
                ReadingPartFours = ReadingTestPaper.GeneratePart4(_TestCategoryManager)
            };
        }
        public static int GenerateReadingTestPaper(this TestCategoryManager _TestCategoryManager, PieceOfTestManager _PieceOfTestManager, int UserId, int? InstructorId)
        {
            // Kiến tạo danh sách câu hỏi và câu trả lời, đồng thời xáo trộn câu trả lời
            ReadingTestPaper paper = _TestCategoryManager.GenerateReadingTestPaper();
            // Khởi tạo đối tượng lưu trữ bài kiểm tra này và lưu paper mặc định có đáp án đúng vào
            var piceOfTest = new PieceOfTest
            {
                UserId = UserId,
                InstructorId = InstructorId,
                TypeCode = TestCategory.READING,
                PartId = -1,
                ResultOfTestJson = JsonConvert.SerializeObject(paper),
            };
            // Lưu trữ bài thi vào database trước khi bắt đầu

            return piceOfTest.Id;
        }
    }
}