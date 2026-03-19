import React, { useState, useEffect } from "react";
import AddMember from "@/app/(home)/components/WidgetCards/WidgetCardModals/AddMemberModal";
import { ChevronRightIcon, ChevronLeftIcon } from "@heroicons/react/24/outline";
import { PlusCircleIcon } from "@heroicons/react/24/solid";
import AvatarCell from "../../Avatar";

interface Person {
  name: string;
  email: string;
  image?: string;
}

interface AvatarCarouselProps {
  people: Person[];
}

const AvatarCarousel: React.FC<AvatarCarouselProps> = ({ people }) => {
  const [addMemberModal, setAddMemberModal] = useState(false);
  const [avatarsPerPage, setAvatarsPerPage] = useState(1);
  const [currentPage, setCurrentPage] = useState(0);

  useEffect(() => {
    const handleResize = () => {
      const nextAvatarsPerPage = Math.max(
        1,
        Math.floor((window.innerWidth * 0.3 - (24 + 24 + 24 + 32.5)) / 50),
      );

      setAvatarsPerPage(nextAvatarsPerPage);
    };

    window.addEventListener("resize", handleResize);
    handleResize(); // Initially sets avatar per page

    return () => window.removeEventListener("resize", handleResize);
  }, []);

  useEffect(() => {
    setCurrentPage(0);
  }, [people.length, avatarsPerPage]);

  const totalPages = Math.ceil(people.length / avatarsPerPage);

  const handleNext = () => {
    if (currentPage < totalPages - 1) {
      setCurrentPage(currentPage + 1);
    }
  };

  const handlePrev = () => {
    if (currentPage > 0) {
      setCurrentPage(currentPage - 1);
    }
  };

  const startIdx = currentPage * avatarsPerPage;
  const endIdx = startIdx + avatarsPerPage;
  const currentAvatars = people.slice(startIdx, endIdx);

  return (
    <div className="flex items-center justify-center w-full">
      <div className="flex items-center justify-between round-box pt-4 pb-4 w-full">
        <div className="flex items-center space-x-2 w-full">
          {/* Left Button */}
          <div className="flex items-center">
            <button onClick={handlePrev} className="flex-shrink-0">
              <ChevronLeftIcon className="size-6" />
            </button>
          </div>

          {/* Avatar Icons */}
          <div className="flex items-center justify-center space-x-3">
            {currentAvatars.map((person, index) => (
              <div key={index} className="avatar inline-block">
                <div>
                  <AvatarCell
                    name={person.name}
                    image={person.image}
                    showName={false}
                  />
                </div>
              </div>
            ))}
            <button onClick={() => setAddMemberModal(true)}>
              <PlusCircleIcon className="w-10 h-10 text-secondary" />
            </button>
          </div>

          {/* Right Button */}
          <div className="flex items-center mr-4">
            <button onClick={handleNext} className="flex-shrink-0">
              <ChevronRightIcon className="size-6" />
            </button>
          </div>
        </div>
      </div>

      {/* Add Member Modal */}
      <AddMember
        isOpen={addMemberModal}
        onClose={() => setAddMemberModal(false)}
      />
    </div>
  );
};

export default AvatarCarousel;
