using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using PaymentAPI.Models;
using Spire.Doc;
using System.IO;


namespace PaymentAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentDetailController : ControllerBase
    {
        #region Property  
        const string CONTENT_TYPE_ZIP = "application/zip";  // MIME type
        const string CONTENT_TYPE_FILE = "application/octet-stream";  // MIME type
        const string N_A = "N/A";
        const string MEDIA_ROOT_FOLDER = "Upload";
        private readonly string _Url;
        private readonly string _ApiUpload = @"/api/Media/UploadFile2";
        private readonly string _ApiRemove = @"/api/Media/RemoveUploadFile";
        private readonly string _ApiRename = @"/api/Media/RenameFile";
        private readonly string _ApiRemoveMultiple = @"/api/Media/RemoveUploadMultiple";
        #endregion

        private readonly PaymentDetailContext _context;

        public PaymentDetailController(PaymentDetailContext context)
        {
            _context = context;
        }

        // GET: api/PaymentDetail
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PaymentDetail>>> GetPaymentDetails()
        {
          if (_context.PaymentDetails == null)
          {
              return NotFound();
          }
            return await _context.PaymentDetails.ToListAsync();
        }

        // GET: api/PaymentDetail/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentDetail>> GetPaymentDetail(int id)
        {
          if (_context.PaymentDetails == null)
          {
              return NotFound();
          }
            var paymentDetail = await _context.PaymentDetails.FindAsync(id);

            if (paymentDetail == null)
            {
                return NotFound();
            }

            return paymentDetail;
        }

        // PUT: api/PaymentDetail/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPaymentDetail(int id, PaymentDetail paymentDetail)
        {
            if (id != paymentDetail.PaymentDetailID)
            {
                return BadRequest();
            }

            _context.Entry(paymentDetail).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PaymentDetailExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Ok(await _context.PaymentDetails.ToListAsync());
        }

        // POST: api/PaymentDetail
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<PaymentDetail>> PostPaymentDetail(PaymentDetail paymentDetail)
        {
          if (_context.PaymentDetails == null)
          {
              return Problem("Entity set 'PaymentDetailContext.PaymentDetails'  is null.");
          }
            _context.PaymentDetails.Add(paymentDetail);
            await _context.SaveChangesAsync();

            return Ok(await _context.PaymentDetails.ToListAsync());
        }

        [HttpPost("{type}/convert")]
        public async Task<ActionResult<List<MediaViewModel>>> ConvertFileAsync(List<IFormFile> data, string subDirectory)
        {
            List<(IFormFile formFile, byte[] fileData)> pdfFiles = new();

            if (data.Count <= 0)
            {
                return Ok();
            }
            //subDirectory = Path.Combine(path1: "~",
            //                path2: MEDIA_ROOT_FOLDER,
            //                path3: subDirectory ?? string.Empty);
            subDirectory = "~\\Upload\\ChuKySo/TrangBiaHocBa/1034/143/5/10/20000";
            var _Url2 = "https://commonmedia.quanlygiaoduc.vn/";
            var _ApiUpload2 = "/api/Media/UploadFile2";
            var targetPath = new Uri(baseUri: new(_Url2), relativeUri: _ApiUpload2);


            #region Convert doc to pdf
            foreach (var file in data)
            {
                if (file.ContentType == "application/msword" || file.ContentType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
                {
                    using var stream = file.OpenReadStream();

                    using (MemoryStream memoryStream = new())
                    {
                        // Copy file stream to memory stream
                        await stream.CopyToAsync(memoryStream);
                        memoryStream.Position = 0;

                        // Load document
                        Document document = new Document();
                        document.LoadFromStream(memoryStream, FileFormat.Docx);

                        // Create a unique file name for the PDF
                        string uniqueFileName = $"{Guid.NewGuid().ToString()}.pdf";
                        string filePath = Path.Combine(Path.GetTempPath(), uniqueFileName);

                        // Save document as PDF
                        document.SaveToFile(filePath, FileFormat.PDF);

                        // Create an IFormFile instance for the PDF file
                        byte[] pdfBytes = System.IO.File.ReadAllBytes(filePath);
                        //var pdfMemoryStream = new MemoryStream(pdfBytes);
                        //IFormFile pdfFile = new FormFile(pdfMemoryStream, 0, pdfMemoryStream.Length, null, Path.GetFileNameWithoutExtension(file.FileName) + ".pdf");

                        // Create an IFormFile instance for the PDF file with appropriate parameters
                        using (var streamx = System.IO.File.OpenRead(filePath))
                        {
                            var filex = new FormFile(streamx, 0, streamx.Length, null, Path.GetFileNameWithoutExtension(file.FileName) + ".pdf")
                            {
                                Headers = new HeaderDictionary(),
                                ContentType = "application/pdf"
                            };
                            // Add the PDF file to the list
                            pdfFiles.Add((filex, pdfBytes));
                            streamx.Close();
                        }


                        // Clean up: delete temporary PDF file
                        System.IO.File.Delete(filePath);

                        memoryStream.Close();

                    }



                }
                else if (file.ContentType == "application/pdf")
                {
                    if (file.Length <= 0)
                        continue;
                    using (MemoryStream memoryStream = new())
                    {
                        await file.CopyToAsync(memoryStream);
                        pdfFiles.Add((file, memoryStream.ToArray()));
                        memoryStream.Close();
                    }
                }

            }
            #endregion

            #region Upload file
            if (pdfFiles != null && pdfFiles.Any())
            {
                List<(string key, IFormFile file, byte[] fileData)> fileData = new List<(string key, IFormFile file, byte[] fileData)>();

                foreach (var file in pdfFiles)
                {
                    string key = Path.GetFileNameWithoutExtension(file.formFile.FileName) ?? Guid.NewGuid().ToString();
                    fileData.Add((key: key, file: file.formFile!, fileData: file.fileData));
                }

                using (HttpClient client = new HttpClient())
                {
                    MultipartFormDataContent content = new MultipartFormDataContent();

                    StringContent strFolderName = new StringContent(content: subDirectory);
                    content.Add(content: strFolderName,
                                name: "pFolder");

                    List<MediaViewModel> result = new List<MediaViewModel>();

                    int i = 0;
                    fileData.ForEach((item) =>
                    {
                        string newFileName = $"{item.key}{Path.GetExtension(item.file.FileName)}";
                        result.Add(new()
                        {
                            TenFile = item.file.FileName,
                            DungLuong = item.fileData.Length,
                            TenFileMoi = newFileName,
                            Key = item.key,
                            DinhDang = Path.GetExtension(item.file.FileName)
                        });

                        ByteArrayContent baContent = new ByteArrayContent(content: item.fileData);
                        content.Add(content: baContent,
                                    name: $"File{i++}",
                                    fileName: Uri.EscapeDataString(newFileName));
                    });

                    var response = await client.PostAsync(requestUri: targetPath,
                                                          content: content);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = response.Content.ReadAsStringAsync().Result;

                        var listData = JsonConvert.DeserializeObject<List<MediaViewModel>>(responseBody);

                        // Map the two lists based on the Id field
                        var mappedList = result.Join(inner: listData,
                                                     outerKeySelector: l1 => l1.TenFileMoi,
                                                     innerKeySelector: l2 => l2.TenFileMoi,
                                                     resultSelector: (l1, l2) => new MediaViewModel()
                                                     {
                                                         TenFile = l1.TenFile,
                                                         DungLuong = l1.DungLuong,
                                                         DuongDan = l2.DuongDan,
                                                         DuongDanExtract = l2.DuongDanExtract,
                                                         TenFileMoi = l2.TenFileMoi,
                                                         Key = l1.Key,
                                                         DinhDang = l1.DinhDang
                                                     }).ToList();

                        return mappedList;
                    }
                }

            }

            #endregion

            return Ok();
        }

        // DELETE: api/PaymentDetail/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePaymentDetail(int id)
        {
            if (_context.PaymentDetails == null)
            {
                return NotFound();
            }
            var paymentDetail = await _context.PaymentDetails.FindAsync(id);
            if (paymentDetail == null)
            {
                return NotFound();
            }

            _context.PaymentDetails.Remove(paymentDetail);
            await _context.SaveChangesAsync();

            return Ok(await _context.PaymentDetails.ToListAsync());
        }

        private bool PaymentDetailExists(int id)
        {
            return (_context.PaymentDetails?.Any(e => e.PaymentDetailID == id)).GetValueOrDefault();
        }
    }
}
