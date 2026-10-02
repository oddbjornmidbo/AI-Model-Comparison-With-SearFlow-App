import type { Meta, StoryObj } from '@storybook/react-vite'
import { participants, schedule } from '../fixtures'
import { ScheduleView } from './ScheduleView'

const meta = {
  title: 'Components/ScheduleView',
  component: ScheduleView,
  args: { participants, selectedId: 3, schedule, onSelect: () => {} },
} satisfies Meta<typeof ScheduleView>
export default meta
type Story = StoryObj<typeof meta>

export const Populated: Story = {}
export const NoParticipantSelected: Story = { args: { selectedId: null, schedule: undefined } }
export const Loading: Story = { args: { schedule: undefined, loading: true } }
export const Empty: Story = { args: { schedule: { participant: participants[2], registrations: [] } } }
export const Error: Story = { args: { schedule: undefined, error: 'Participant not found.', onRetry: () => {} } }
export const LoadingParticipants: Story = { args: { participants: undefined, selectedId: null, schedule: undefined } }
