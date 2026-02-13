using OmniArchivum.Api.Models.DTOs;

namespace OmniArchivum.Api.Services;

public interface INotesService
{
    Task<List<NoteResponse>> GetAllAsync(int page, int pageSize);
    Task<NoteResponse?> GetByIdAsync(Guid id);
    Task<NoteResponse> CreateAsync(CreateNoteRequest request);
    Task<bool> SoftDeleteAsync(Guid id);
    Task<List<NoteResponse>> SearchAsync(string query, int page, int pageSize);
}
