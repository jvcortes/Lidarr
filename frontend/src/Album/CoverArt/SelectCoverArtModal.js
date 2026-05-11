import PropTypes from 'prop-types';
import React from 'react';
import Modal from 'Components/Modal/Modal';
import SelectCoverArtModalConnector from './SelectCoverArtModalConnector';

function SelectCoverArtModal(props) {
  const {
    isOpen,
    albumId,
    albumTitle,
    onModalClose
  } = props;

  return (
    <Modal
      isOpen={isOpen}
      onModalClose={onModalClose}
    >
      <SelectCoverArtModalConnector
        albumId={albumId}
        albumTitle={albumTitle}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

SelectCoverArtModal.propTypes = {
  isOpen: PropTypes.bool.isRequired,
  albumId: PropTypes.number.isRequired,
  albumTitle: PropTypes.string.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default SelectCoverArtModal;
