using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VpuSimulator.Application.Interfaces;

// Điểm duy nhất upload ảnh/crop sự kiện lên Object Storage (SeaweedFS / S3 / MinIO)
public interface IObjectStorage
{
    Task<string> UploadAsync(string objectKey, Stream content, CancellationToken cancellationToken = default);
}
