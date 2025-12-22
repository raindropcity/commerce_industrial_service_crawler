using Microsoft.AspNetCore.Mvc;

namespace Crawlers.BusinessLogics.Services.Interfaces;

public interface ICompaniesDataService
{
    /// <summary>
    /// 取得經濟部商工登記工示公司資料
    /// </summary>
    /// <param name="csvFile">傳入 csv 檔案(內容為公司名稱列表)</param>
    /// <returns>回傳查詢結果的 csv檔案</returns>
    Task<FileStreamResult> GetByCsvFileAsync(IFormFile csvFile);
}
