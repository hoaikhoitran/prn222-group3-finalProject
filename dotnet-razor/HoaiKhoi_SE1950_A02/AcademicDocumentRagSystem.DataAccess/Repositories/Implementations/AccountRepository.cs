using AcademicDocumentRagSystem.DataAccess.Models;
using AcademicDocumentRagSystem.DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcademicDocumentRagSystem.DataAccess.Repositories.Implementations
{
    public class AccountRepository : IAccountRepository
    {
        private readonly AcademicRagDbContext _context;
        public AccountRepository(AcademicRagDbContext context)
        {
            _context = context;
        }
        public async Task<Account?> GetByEmailAndPasswordAsync(string email, string password)
        {
            return await _context.Accounts
                .Include(a => a.TeachingCourses)
                .FirstOrDefaultAsync(a => a.Email == email && a.Password == password && a.Status);
        }

        public async Task<List<Account>> GetAllAsync(string? searchTerm, int? role, bool? status)
        {
            var query = _context.Accounts
                .Include(a => a.TeachingCourses)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var keyword = searchTerm.Trim();
                query = query.Where(a =>
                    a.Email.Contains(keyword) ||
                    a.FullName.Contains(keyword));
            }

            if (role.HasValue)
            {
                query = query.Where(a => a.Role == role.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(a => a.Status == status.Value);
            }

            return await query
                .OrderBy(a => a.Role)
                .ThenBy(a => a.FullName)
                .ToListAsync();
        }

        public async Task<Account?> GetByIdAsync(int id)
        {
            return await _context.Accounts
                .Include(a => a.TeachingCourses)
                .FirstOrDefaultAsync(a => a.AccountId == id);
        }

        public async Task<Account?> GetByEmailAsync(string email)
        {
            return await _context.Accounts.FirstOrDefaultAsync(a => a.Email == email);
        }

        public async Task<Account?> GetByEmailExceptIdAsync(string email, int accountId)
        {
            return await _context.Accounts.FirstOrDefaultAsync(a => a.Email == email && a.AccountId != accountId);
        }

        public async Task AddAsync(Account account)
        {
            await _context.Accounts.AddAsync(account);
        }

        public void Update(Account account)
        {
            _context.Accounts.Update(account);
        }

        public void Delete(Account account)
        {
            _context.Accounts.Remove(account);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
