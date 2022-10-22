using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers
{
    [Authorize]
    public class UsersController : BaseApiController
    {
        private readonly IUserRepository userRepository;
        private readonly IMapper mapper;
        public IPhotoService PhotoService;
        
        public UsersController(IUserRepository userRepository, IMapper mapper, IPhotoService photoService) 
        {
            this.PhotoService = photoService;
            this.mapper = mapper;
            this.userRepository = userRepository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MemberDTO>>> GetUsers() 
        {
            var users = await this.userRepository.GetMembersAsync();

            return Ok(users);
        }

        // api/users/3
        [HttpGet("{username}", Name = "GetUser")]
        public async Task<ActionResult<MemberDTO>> GetUser(string username) 
        {
            return await this.userRepository.GetMemberAsync(username);
            
        }

        [HttpPut]
        public async Task<ActionResult> UpdateUser(MemberUpdateDTO memberUpdateDTO) 
        {
            var user = await this.userRepository.GetUserByUsernameAsync(User.GetUsername()); 

            this.mapper.Map(memberUpdateDTO, user);

            this.userRepository.Update(user);

            if (await this.userRepository.SaveAllAsync()) return NoContent();

            return BadRequest("Failed to update user");
        }

        [HttpPost("add-photo")]
        public async Task<ActionResult<PhotoDTO>> AddPhoto(IFormFile file) 
        {
            var user = await this.userRepository.GetUserByUsernameAsync(User.GetUsername()); 

            var result = await this.PhotoService.AddPhotoAsync(file);

            if (result.Error != null) return BadRequest(result.Error.Message);

            var photo = new Photo
            {
                Url = result.SecureUrl.AbsoluteUri,
                PublicId = result.PublicId,
            };

            if (user.Photos.Count == 0) 
            {
                photo.IsMain = true;
            }

            user.Photos.Add(photo);

            if (await this.userRepository.SaveAllAsync()) 
            {
                // return this.mapper.Map<PhotoDTO>(photo);
                return CreatedAtRoute("GetUser", new {userName = user.UserName}, this.mapper.Map<PhotoDTO>(photo));
            }

            return BadRequest("Problem adding Photo");
        }

        [HttpPut("set-main-photo/{photoId}")]
        public async Task<ActionResult> SetMainPhoto(int photoId)
        {
            var user = await this.userRepository.GetUserByUsernameAsync(User.GetUsername());

            var photo = user.Photos.FirstOrDefault(x => x.Id == photoId);

            if (photo.IsMain) return BadRequest("This is already your main photo!");

            var currentMain = user.Photos.FirstOrDefault(x => x.IsMain);
            if(currentMain != null) currentMain.IsMain = false;
            photo.IsMain = true;

            if (await this.userRepository.SaveAllAsync()) return NoContent();

            return BadRequest("Failed to set Main Photo");
        }

        [HttpDelete("delete-photo/{photoId}")]
        public async Task<ActionResult> DeletePhoto(int photoId) 
        {
            var user = await this.userRepository.GetUserByUsernameAsync(User.GetUsername());

            var photo = user.Photos.FirstOrDefault(x => x.Id == photoId);

            if (photo == null) return NotFound();

            if (photo.IsMain) return BadRequest("You cannot delete your main Photo!");

            if (photo.PublicId != null)
            {
                var result = await this.PhotoService.DeletePhotoAsync(photo.PublicId);
                if (result.Error != null) return BadRequest(result.Error.Message);
            }

            user.Photos.Remove(photo);

            if (await this.userRepository.SaveAllAsync()) return Ok();

            return BadRequest("Failed to Delete the Photo");
        } 
    }
}